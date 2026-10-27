using System;
using System.Collections.Generic;
using System.Linq;
using MfmeFmlDecoder.src.Model.Component;

namespace MfmeFmlDecoder.src.Decoder.Component.Helper
{
    /// <summary>
    /// Assigns candidate components to border containers by containment,
    /// sibling local-offset templates, and ordinal order when copies compete.
    /// See docs/border-lamp-assignment-report.html.
    /// </summary>
    internal static class BorderOwnershipAssigner
    {
        internal readonly record struct Offset(int Dx, int Dy);

        /// <summary>
        /// Known sibling template for the 39×75 border examples in the assignment report.
        /// Used when <paramref name="template"/> is omitted and discovery finds nothing.
        /// </summary>
        private static readonly Offset[] DefaultTemplate =
        {
            new Offset(1, 1),
            new Offset(21, 57),
        };

        internal sealed class Result
        {
            public IReadOnlyDictionary<Border, IReadOnlyList<BaseComponent>> Assignment { get; init; }
            public IReadOnlyList<BaseComponent> Orphans { get; init; }
            public IReadOnlyList<BaseComponent> Unresolved { get; init; }
        }

        /// <summary>
        /// Assigns layout ordinals, then annotates each <see cref="Border"/> with the
        /// ordinals of components it owns. No-op when the layout has no borders.
        /// </summary>
        public static void Annotate(IList<BaseComponent> components)
        {
            if (components is null)
            {
                throw new ArgumentNullException(nameof(components));
            }

            for (int i = 0; i < components.Count; i++)
            {
                components[i].OrdinalComponentIdentifier = i;
            }

            List<Border> borders = components.OfType<Border>().ToList();
            if (borders.Count == 0)
            {
                return;
            }

            // Borders are containers only; nested-border ownership can be added later.
            List<BaseComponent> candidates = components.Where(c => c is not Border).ToList();
            Result result = Assign(borders, candidates);

            foreach (Border border in borders)
            {
                border.OwnedOrdinalComponentIdentifiers = result.Assignment[border]
                    .Select(c => c.OrdinalComponentIdentifier!.Value)
                    .OrderBy(id => id)
                    .ToArray();
            }
        }

        public static Result Assign(
            IReadOnlyList<Border> borders,
            IReadOnlyList<BaseComponent> candidates,
            IReadOnlyList<Offset> template = null)
        {
            if (borders is null)
            {
                throw new ArgumentNullException(nameof(borders));
            }

            if (candidates is null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            var assignment = borders.ToDictionary(b => b, _ => new List<BaseComponent>());
            var unmatched = new HashSet<BaseComponent>(candidates);
            var orphans = new List<BaseComponent>();

            // 1. Orphans — contained by no border
            foreach (BaseComponent component in candidates.OrderBy(c => c.OrdinalComponentIdentifier))
            {
                if (!borders.Any(b => Contains(b, component)))
                {
                    orphans.Add(component);
                    unmatched.Remove(component);
                }
            }

            // 2. Unique containment
            foreach (BaseComponent component in unmatched.OrderBy(c => c.OrdinalComponentIdentifier).ToList())
            {
                List<Border> owners = borders.Where(b => Contains(b, component)).ToList();
                if (owners.Count == 1)
                {
                    assignment[owners[0]].Add(component);
                    unmatched.Remove(component);
                }
            }

            // 3. Competing copies — fill template slots by rising border ordinal.
            //    Do NOT require component.ordinal < border.ordinal.
            foreach (IGrouping<(uint Width, uint Height), Border> sizeGroup in borders.GroupBy(b => (b.Width, b.Height)))
            {
                List<Border> groupBorders = sizeGroup.ToList();
                Offset[] slots = ResolveTemplate(template, groupBorders, assignment, unmatched);

                foreach (Border border in groupBorders.OrderBy(b => b.OrdinalComponentIdentifier))
                {
                    foreach (Offset slot in slots)
                    {
                        BaseComponent match = unmatched
                            .Where(c => Contains(border, c) && LocalOffset(border, c).Equals(slot))
                            .OrderBy(c => c.OrdinalComponentIdentifier)
                            .FirstOrDefault();

                        if (match is null)
                        {
                            continue;
                        }

                        assignment[border].Add(match);
                        unmatched.Remove(match);
                    }
                }
            }

            return new Result
            {
                Assignment = assignment.ToDictionary(
                    kvp => kvp.Key,
                    kvp => (IReadOnlyList<BaseComponent>)kvp.Value),
                Orphans = orphans,
                Unresolved = unmatched.OrderBy(c => c.OrdinalComponentIdentifier).ToList(),
            };
        }

        public static Offset LocalOffset(BaseComponent border, BaseComponent child) =>
            new Offset(checked((int)child.X - (int)border.X), checked((int)child.Y - (int)border.Y));

        public static bool Contains(BaseComponent border, BaseComponent other)
        {
            long bx = border.X;
            long by = border.Y;
            long bw = border.Width;
            long bh = border.Height;
            long ox = other.X;
            long oy = other.Y;
            long ow = other.Width;
            long oh = other.Height;

            return ox >= bx
                && oy >= by
                && ox + ow <= bx + bw
                && oy + oh <= by + bh;
        }

        private static Offset[] ResolveTemplate(
            IReadOnlyList<Offset> explicitTemplate,
            IReadOnlyList<Border> sizeGroup,
            IReadOnlyDictionary<Border, List<BaseComponent>> assignment,
            HashSet<BaseComponent> unmatched)
        {
            if (explicitTemplate is not null)
            {
                return explicitTemplate.ToArray();
            }

            var fromUnique = new HashSet<Offset>();
            foreach (Border border in sizeGroup)
            {
                foreach (BaseComponent child in assignment[border])
                {
                    fromUnique.Add(LocalOffset(border, child));
                }
            }

            if (fromUnique.Count > 0)
            {
                return fromUnique.OrderBy(o => o.Dx).ThenBy(o => o.Dy).ToArray();
            }

            Border reference = sizeGroup.OrderBy(b => b.OrdinalComponentIdentifier).First();
            Offset[] discovered = unmatched
                .Where(c => Contains(reference, c))
                .Select(c => LocalOffset(reference, c))
                .Distinct()
                .OrderBy(o => o.Dx)
                .ThenBy(o => o.Dy)
                .ToArray();

            return discovered.Length > 0 ? discovered : DefaultTemplate;
        }
    }
}
