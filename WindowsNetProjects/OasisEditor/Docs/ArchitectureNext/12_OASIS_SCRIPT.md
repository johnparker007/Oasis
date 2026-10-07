# Oasis Script language design

## Purpose and placement

Oasis Script is the small, event-oriented language for deterministic arcade-machine
behaviour. Its canonical source extension is `.oasis`; examples use
`behavior.oasis`. The canonical implementation lives in the pure managed,
`netstandard2.1` `Oasis.Scripting` project. It has no WPF, Unity, filesystem,
networking, reflection, CLR interop, or host-object dependency, so Editor and Player
can consume one lexer/parser/type-checker implementation.

Machine authored schema 8 separates Runtime from Behaviour: Oasis requires one
package-local `behavior.oasis`; Emulation forbids behaviour. A8.3 implements source
packaging (Machine runtime schema 9) and pure interpretation; the A7 host adapter
remains A8.4. Canonical source is in `Oasis.Scripting/Package/Runtime/`, compiled by
the .NET project and Player's local UPM package without source duplication.

The Editor compiles the current buffer after edits and, only after core success, runs a
Machine-domain syntax-tree validator for all typed reference literals. Core and Machine
diagnostics share one line/column list while retaining distinct `OS...` and `OSM3...`
codes. Temporary invalid text is retained and marks the Machine dirty; a saved package
must compile and resolve. Add/Remove participate in document undo, while normal source
typing uses local text undo. Player compiles packaged source once at load; normal preview stops at the missing A8.4 adapter.
Machine reference discovery follows assembled composition: authored Object3D/anchor/input
and reel identities, semantic triggers from the resolved Cabinet GLB, and linked
lamp/alpha/seven-segment references from assigned Faces. Missing composition assets and a
missing declared `behavior.oasis` are explicit authoring diagnostics, not empty domains or
implicitly valid empty programs.

## Lexical syntax and diagnostics

Source is UTF-8 text. Identifiers, decimal number literals, quoted strings, and
`//` line comments are supported. Reserved words are exactly `const`, `state`,
`let`, `on`, `if`, `else`, `for`, `in`, `true`, `false`, `and`, `or`, and `not`.
Statements end with semicolons; blocks use braces.

The hand-written lexer records absolute offsets plus one-based line and column.
The recursive-descent parser uses precedence climbing for expressions. Ordinary
source errors produce structured diagnostics containing stable code, severity,
message, source name, and span, for example `behavior.oasis:12:9`. `OS1xxx`
identifies lexical/syntax errors, `OS2xxx` semantic errors, `OS21xx` type/control
errors, `OS22xx` event errors, `OS23xx` built-in errors, `OS24xx` reference errors,
and `OS25xx` bounded-range errors.

## Types and values

The primitive types are `Number`, `Bool`, `String`, and `Vec3`. There are distinct
`ObjectRef`, `AnchorRef`, `TriggerRef`, `InputRef`, `LampRef`, `ReelRef`,
`AlphaDisplayRef`, and `SevenSegmentRef` types. `List<T>` is immutable and
homogeneous; an empty list is rejected because its element type cannot be inferred.
There is no `Any`, implicit conversion, object/map, or list mutation.

Typed references are literals rather than strings:

```oasis
object:ball08
anchor:traySlot01
trigger:PocketLeftCorner
input:rerack
lamp:17
reel:2
alpha:0
sevenSegment:12
```

Text IDs use the existing conservative ASCII letter/digit/underscore/hyphen rule.
Device IDs are non-negative decimal integers. A8.1 validates syntax and type only;
existence in a Machine is deliberately deferred to A8.2.

`vec3(x, y, z)` requires three Numbers and returns `Vec3`. V1 defines no vector
operators. Arithmetic and ordered comparisons require Number, equality requires
identical types, and boolean operators require Bool. Precedence, from high to low,
is unary, multiplicative, additive, ordered comparison, equality, `and`, then `or`.

## Declarations and assignment

`const` and `state` are top-level, require initializers, and infer fixed types.
Declarations are analyzed in source order. A const initializer may reference only
previously declared const values, and a state initializer may likewise reference
only previously declared const values. Neither kind of top-level initializer may
reference mutable state. Later declarations are unavailable and there is no
dependency sorting. `state` represents Machine-session state and is the only
assignable symbol. `let` introduces an immutable, inferred, block-scoped local.

V1 deliberately forbids shadowing: an event binding, loop variable, or local may
not shadow any active local/binding or global. Duplicate names in one scope are
also errors. This conservative policy makes authored behaviour unambiguous.

## Control flow and bounded iteration

`if`/`else` requires a Bool condition and creates nested scopes. The only loop is:

```oasis
for ball in balls { object.reset(ball); }
for i in range(0, 16) { let ball = balls[i]; }
```

The iterable must be `List<T>` or the dedicated bounded range type. `range(start,
end)` is start-inclusive and end-exclusive. In V1 its bounds must be finite,
integral compile-time number literals (unary minus is recognized); `start <= end`
is required, so equal bounds are empty and reversed bounds are a semantic error.
List indexes have Number type; literal indexes are additionally checked as integral,
and the interpreter enforces integrality and bounds for computed values.
There is no `while`, `loop`, `break`, or `continue`.

## Event patterns

Event handlers are declarations retained in source order. Multiple handlers for the
same event are valid and dispatch executes matching handlers in that order.
Each parameter is explicitly represented as either an `EventFilter` literal or an
`EventBinding` with the signature's static type.

The fixed V1 signatures are `machine.started()`, `input.pressed(InputRef)`,
`input.released(InputRef)`, `trigger.entered(TriggerRef, ObjectRef)`,
`trigger.exited(TriggerRef, ObjectRef)`, `collision.entered(ObjectRef, ObjectRef)`,
`collision.exited(ObjectRef, ObjectRef)`, and `timer.elapsed(String)`.

```oasis
on trigger.entered(trigger:PocketLeftCorner, ball) {
    object.reset(ball);
}

on timer.elapsed(timerId) {
    timer.stop(timerId);
}
```

## Built-in registry

Calls are resolved through one declarative signature registry, not parser special
cases. Value built-ins are `vec3(Number, Number, Number) -> Vec3` and
`range(Number, Number) -> Range`. Initial Void commands are:

- `object.set_active(ObjectRef, Bool)`;
- `object.teleport(ObjectRef, AnchorRef)`;
- `object.teleport_pose(ObjectRef, Vec3, Vec3)`;
- `object.set_velocity`, `object.set_angular_velocity`, and
  `object.apply_impulse` with `(ObjectRef, Vec3)`;
- `object.reset(ObjectRef)`;
- `timer.start(String, Number)` and `timer.stop(String)`.

Namespaced calls are language names, not member access. Void calls are statements
and cannot supply a value. Lamp/reel/display commands are deferred to A8.6.

## Program and execution boundary

`OasisScriptCompiler.Compile(sourceText, sourceName)` returns a compilation result
with `Success`, diagnostics, and a program only after syntax and semantics succeed.
The immutable syntax tree has explicit program, declaration, block, statement,
event parameter, and expression node types, all with spans. The semantic program
contains typed constants/states and source-ordered, typed event handlers while
retaining the validated AST for the interpreter.

`OasisScriptSession(program, host, options)` initializes constants in source order,
then state initializers in source order, exactly once. Compiled programs have no
mutable session state, and sessions are independent. Immutable explicit runtime
values represent Number (finite double), Bool, String, Vec3, domain-typed references,
List, Range and Void. Lists copy their inputs and expose read-only collections.
Equality is typed value equality, including component-wise vectors and structural
lists/ranges. Only state assignment mutates a value; const/let/event/loop bindings
are immutable. `TryGetState`/`TryGetConstant` provide read-only diagnostic access.

`Dispatch(OasisScriptEvent.MachineStarted())`, `TriggerEntered(triggerRef, objectRef)`,
and the other fixed event factories validate argument domains. Literal filters use
exact typed equality (ordinal, case-sensitive IDs/strings). Matching handlers run
in declaration order with fresh local scopes and shared session state. Nested
blocks and each loop iteration have their own immutable bindings. Boolean `and`
and `or` short-circuit. Ranges are lazy start-inclusive/end-exclusive iterations,
never allocated lists. Bounds remain finite integral Numbers as in A8.1; an exact
integer cursor ensures progress even above double's unit-resolution domain. Each
binding is converted to a Number, with the usual double precision. Malformed
non-finite/non-integral/reversed bounds are rejected.

`IOasisScriptHost` returns `OasisScriptHostResult` for SetActive, Teleport,
TeleportPose, SetVelocity, SetAngularVelocity, ApplyImpulse, ResetObject, StartTimer,
and StopTimer. References retain their domain Type, vectors are pure numeric values,
and hosts never receive CLR script objects. `vec3` and `range` execute internally.
Explicit failed host results become diagnostics; unexpected host exceptions are
also converted defensively. The scripting assembly implements no object/timer logic.

A deterministic budget comes from positive `OasisScriptExecutionOptions.MaxInstructionsPerEvent`
(default 10,000). Charge one instruction for every statement (including each block),
every expression visited, each loop iteration, and each built-in invocation in
addition to evaluating its arguments. Charge one handler attempt for the matching
event name, including empty bodies and rejected literal filters. Initialization
gets a separate budget of the same size and charges each global declaration and
its expression tree. Skipped branches/RHS expressions consume nothing; each event
starts with a fresh budget shared by all its handlers. Dispatch is synchronous and
non-reentrant. Hosts must not synchronously redispatch into the same session.

Runtime diagnostics carry code, message, source name, span/line/column, and event
name (or initialization). Stable runtime codes are:

| Code | Failure |
| --- | --- |
| OSR1001 | Instruction limit exceeded |
| OSR1002 | Division/modulo by zero |
| OSR1003 | Non-finite arithmetic |
| OSR1004 | Non-integral/out-of-bounds list index |
| OSR1005 | Host command failure |
| OSR1006 | Invalid runtime range |
| OSR1007 | Invalid compiled operation |

The first initialization/dispatch failure sets `IsFaulted` and
`LastRuntimeDiagnostic`. `Dispatch` returns false; every later attempt executes
nothing and retains the original error, avoiding repeat reports. State and completed
host effects before the fault are retained for diagnostics; execution is not a
transaction and effects are not rolled back. No A7 subscriptions, commands, timer
adapter or startup event emission are implemented here. A8.4 supplies those adapters
and consumes the fault policy. Random remains deferred.

## Representative program

```oasis
const balls = [object:ball01, object:ball02];
const anchors = [anchor:rackBall01, anchor:rackBall02];
state score = 0;

on input.pressed(input:rerack) {
    for i in range(0, 2) {
        object.teleport(balls[i], anchors[i]);
        object.set_velocity(balls[i], vec3(0, 0, 0));
    }
    score = 0;
}
```

## Deliberate V1 exclusions

V1 has no user functions, recursion, classes, maps, exceptions, threads,
coroutines, modules/imports, lambdas, closures beyond event-bound values,
inheritance, macros, eval, dynamic loading, arbitrary host access, or random API.
These omissions are language safety boundaries, not missing general-purpose
language features.
