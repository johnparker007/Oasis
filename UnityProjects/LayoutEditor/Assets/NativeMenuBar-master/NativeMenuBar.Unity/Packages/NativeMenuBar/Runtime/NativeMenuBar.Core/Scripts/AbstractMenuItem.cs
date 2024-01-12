using System.Collections.Generic;
using UnityEngine;

namespace NativeMenuBar.Core
{
    public abstract class AbstractMenuItem
    {
        [SerializeField]
        private string name = default;
        public string Name
        {
            get => name;
            set => name = value;
        }

        [SerializeField]
        private List<MenuItem> menuItems = default;
        public List<MenuItem> MenuItems => menuItems;

        public string FullPath { get; internal set; }

        public uint Id { get; internal set; }

        public IEnumerable<MenuItem> AllChidren
        {
            get
            {
                var ret = new List<MenuItem>();
                foreach (var child in menuItems)
                {
                    ret.Add(child);
                    ret.AddRange(child.AllChidren);
                }
                return ret;
            }
        }
    }
}
