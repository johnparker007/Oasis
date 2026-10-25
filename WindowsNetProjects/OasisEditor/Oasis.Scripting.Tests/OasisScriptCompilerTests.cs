using System.Linq;
using Oasis.Scripting;
using Xunit;

namespace Oasis.Scripting.Tests;

public sealed class OasisScriptCompilerTests
{
    private static OasisScriptCompilationResult Compile(string source) => OasisScriptCompiler.Compile(source, "behavior.oasis");
    private static void Valid(string source) { var result = Compile(source); Assert.True(result.Success, string.Join("\n", result.Diagnostics)); Assert.NotNull(result.Program); }
    private static void Invalid(string source, string code) { var result = Compile(source); Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Code == code); Assert.Null(result.Program); }

    [Fact] public void EmptySourceCompiles() => Valid("");
    [Fact] public void CommentsCompile() => Valid("// hello\n// world");
    [Fact] public void ConstDeclarationCompiles() => Valid("const answer = 42;");
    [Fact] public void StateDeclarationCompiles() => Valid("state active = false;");
    [Fact] public void EventHandlerCompiles() => Valid("on machine.started { }");
    [Fact] public void NestedBlocksCompile() => Valid("on machine.started { { let x = 1; } }");
    [Fact] public void IfElseCompiles() => Valid("on machine.started { if true { let x = 1; } else { let y = 2; } }");
    [Fact] public void ForListCompiles() => Valid("const xs=[1,2]; on machine.started { for x in xs { let y=x+1; } }");
    [Fact] public void RangeCompiles() => Valid("on machine.started { for i in range(0,3) { let x=i; } }");
    [Fact] public void NamespacedCallCompiles() => Valid("on machine.started { timer.start(\"a\", 1); }");
    [Fact] public void ReferenceLiteralCompiles() => Valid("const x=oasis:ball01;");
    [Fact] public void LegacyObjectReferenceAndBuiltinAliasesAreRejected() { Assert.False(Compile("const x=object:ball;").Success); Assert.False(Compile("on machine.started { object.reset(oasis:x); }").Success); }
    [Fact] public void SyntaxErrorHasLocation() { var d = Compile("\nconst x = ;").Diagnostics.First(x => x.Code.StartsWith("OS1")); Assert.Equal("behavior.oasis", d.SourceName); Assert.Equal(2, d.Line); Assert.True(d.Column > 0); }

    [Fact] public void NumberArithmeticIsValid() => Valid("const x=1+2*3-4/2%1;");
    [Fact] public void BoolOperatorsAreValid() => Valid("const x=true and not false or true;");
    [Fact] public void NumberBoolArithmeticIsRejected() => Invalid("const x=1+true;", "OS2101");
    [Fact] public void NumberComparisonIsValid() => Valid("const x=1<2; const y=2>=1;");
    [Fact] public void ReferenceEqualityIsValid() => Valid("const x=oasis:a==oasis:b;");
    [Fact] public void IncompatibleEqualityIsRejected() => Invalid("const x=1==true;", "OS2101");
    [Fact] public void Vec3IsValid() => Valid("const x=vec3(1,2,3);");
    [Fact] public void Vec3BadArgumentIsRejected() => Invalid("const x=vec3(1,true,3);", "OS2303");
    [Fact] public void HomogeneousListIsValid() => Valid("const x=[anchor:a,anchor:b];");
    [Fact] public void MixedListIsRejected() => Invalid("const x=[1,true];", "OS2101");
    [Fact] public void ListIndexIsValid() => Valid("const x=[1,2]; const y=x[0];");
    [Fact] public void BoolListIndexIsRejected() => Invalid("const x=[1]; const y=x[true];", "OS2101");
    [Fact] public void FractionalLiteralIndexIsRejected() => Invalid("const x=[1]; const y=x[0.5];", "OS2101");
    [Fact] public void ConstIsImmutable() => Invalid("const x=1; on machine.started { x=2; }", "OS2102");
    [Fact] public void LetIsImmutable() => Invalid("on machine.started { let x=1; x=2; }", "OS2102");
    [Fact] public void StateAssignmentIsValid() => Valid("state x=1; on machine.started { x=x+1; }");
    [Fact] public void StateWrongTypeIsRejected() => Invalid("state x=1; on machine.started { x=true; }", "OS2101");
    [Fact] public void UnknownIdentifierIsRejected() => Invalid("const x=missing;", "OS2001");
    [Fact] public void DuplicateGlobalIsRejected() => Invalid("const x=1; state x=2;", "OS2002");
    [Fact] public void DuplicateLocalIsRejected() => Invalid("on machine.started { let x=1; let x=2; }", "OS2002");
    [Fact] public void OuterLocalShadowingIsRejected() => Invalid("on machine.started { let x=1; { let x=2; } }", "OS2002");
    [Fact] public void GlobalShadowingIsRejected() => Invalid("const x=1; on machine.started { let x=2; }", "OS2002");
    [Fact] public void ConstInitializerMayReadEarlierConst() => Valid("const baseScore=100; const doubled=baseScore*2;");
    [Fact] public void StateInitializerMayReadEarlierConst() => Valid("const baseScore=100; state score=baseScore;");
    [Fact] public void ConstInitializerCannotReadEarlierState() => Invalid("state score=0; const snapshot=score;", "OS2104");
    [Fact] public void StateInitializerCannotReadEarlierState() => Invalid("state first=1; state second=first+1;", "OS2104");
    [Fact] public void GlobalInitializerCannotReadLaterConst() => Invalid("const later=earlier; const earlier=1;", "OS2001");
    [Fact] public void EventHandlerMayReadAndWriteState() => Valid("state score=0; on machine.started { score=score+1; let current=score; }");
    [Fact] public void StateAssignmentInsideInputHandlerRemainsValid() => Valid("state active=false; on input.pressed(input:start) { active=true; }");

    [Fact] public void MachineStartedSignatureIsValid() => Valid("on machine.started {}");
    [Fact] public void InputLiteralFilterIsValid() => Valid("on input.pressed(input:rerack) {}");
    [Fact] public void InputBindingIsTyped() => Valid("on input.pressed(input) { let same=input==input:start; }");
    [Fact] public void InputBindingCanBeCompared() => Valid("on input.pressed(input) { let same=input==input:start; }");
    [Fact] public void TriggerFilterAndBindingAreValid() => Valid("on trigger.entered(trigger:Pocket, ball) { oasis.reset(ball); }");
    [Fact] public void TriggerBindingsAreTyped() => Valid("on trigger.entered(pocket, ball) { let p=pocket==trigger:Pocket; oasis.reset(ball); }");
    [Fact] public void CollisionBindingsAreTyped() => Valid("on collision.entered(object, other) { let same=object==other; }");
    [Fact] public void TimerStringFilterIsValid() => Valid("on timer.elapsed(\"hide\") {}");
    [Fact] public void TimerBindingIsTyped() => Valid("on timer.elapsed(timerId) { timer.stop(timerId); }");
    [Fact] public void WrongEventArityIsRejected() => Invalid("on machine.started(x) {}", "OS2201");
    [Fact] public void WrongEventFilterTypeIsRejected() => Invalid("on input.pressed(oasis:ball) {}", "OS2201");
    [Fact] public void DuplicateBindingNamesAreRejected() => Invalid("on collision.entered(ball, ball) {}", "OS2002");
    [Fact] public void UnknownEventIsRejected() => Invalid("on machine.stopped {}", "OS2201");
    [Fact] public void NonLiteralEventFilterIsUnavailable() => Invalid("const x=\"a\"; on timer.elapsed(x) {}", "OS2002");
    [Fact] public void MultipleHandlersPreserveSourceOrder() { var p=Compile("on timer.elapsed(\"a\"){} on timer.elapsed(\"b\"){}").Program!; Assert.Equal(new[]{0,1}, p.EventHandlers.Select(x=>x.SourceOrder)); }

    [Fact] public void ObjectTeleportIsValid() => Valid("on machine.started { oasis.teleport(oasis:ball,anchor:rack); }");
    [Fact] public void ObjectTeleportWrongObjectIsRejected() => Invalid("on machine.started { oasis.teleport(anchor:x,anchor:rack); }", "OS2303");
    [Fact] public void ObjectTeleportWrongAnchorIsRejected() => Invalid("on machine.started { oasis.teleport(oasis:x,oasis:rack); }", "OS2303");
    [Fact] public void BuiltinWrongArityIsRejected() => Invalid("on machine.started { oasis.reset(); }", "OS2302");
    [Fact] public void UnknownBuiltinIsRejected() => Invalid("on machine.started { oasis.explode(oasis:x); }", "OS2301");
    [Fact] public void TimerStartIsValid() => Valid("on machine.started { timer.start(\"x\",1.5); }");
    [Fact] public void TimerDurationMustBeNumber() => Invalid("on machine.started { timer.start(\"x\",true); }", "OS2303");
    [Fact] public void VoidCannotInitializeValue() => Invalid("const x=oasis.reset(oasis:x);", "OS2101");
    [Fact] public void SetActiveIsValid() => Valid("on machine.started { oasis.set_active(oasis:x,true); }");
    [Fact] public void TeleportPoseIsValid() => Valid("on machine.started { oasis.teleport_pose(oasis:x,vec3(0,0,0),vec3(0,90,0)); }");
    [Fact] public void ImpulseIsValid() => Valid("on machine.started { oasis.apply_impulse(oasis:x,vec3(1,0,0)); }");

    [Theory]
    [InlineData("oasis:x", OasisScriptTypeKind.OasisObjectRef)] [InlineData("anchor:x", OasisScriptTypeKind.AnchorRef)] [InlineData("trigger:x", OasisScriptTypeKind.TriggerRef)] [InlineData("input:x", OasisScriptTypeKind.InputRef)]
    [InlineData("lamp:17", OasisScriptTypeKind.LampRef)] [InlineData("reel:2", OasisScriptTypeKind.ReelRef)] [InlineData("alpha:0", OasisScriptTypeKind.AlphaDisplayRef)] [InlineData("sevenSegment:12", OasisScriptTypeKind.SevenSegmentRef)]
    [InlineData("prefab:ball", OasisScriptTypeKind.PrefabRef)] [InlineData("surface:table", OasisScriptTypeKind.SurfaceRef)]
    public void ReferenceTypeIdentityIsPreserved(string literal, OasisScriptTypeKind kind) { var p=Compile("const x="+literal+";").Program!; Assert.Equal(kind,p.Constants[0].Type.Kind); }
    [Theory] [InlineData("oasis:")] [InlineData("anchor:a:b")] public void MalformedTextReferenceIsRejected(string value) => Invalid("const x="+value+";", "OS2401");
    [Fact] public void WhitespaceCannotOccurInsideReference() => Assert.False(Compile("const x=oasis:bad id;").Success);
    [Theory] [InlineData("lamp:abc")] [InlineData("reel:-1")] [InlineData("alpha:")] public void MalformedNumericReferenceIsRejected(string value) => Invalid("const x="+value+";", "OS2401");

    [Fact] public void WhileIsNotSyntax() => Assert.False(Compile("on machine.started { while true {} }").Success);
    [Fact] public void BreakIsUnavailable() => Assert.False(Compile("on machine.started { break; }").Success);
    [Fact] public void ContinueIsUnavailable() => Assert.False(Compile("on machine.started { continue; }").Success);
    [Fact] public void ForIterableMustBeBounded() => Invalid("on machine.started { for x in 1 {} }", "OS2103");
    [Fact] public void RangeFractionalBoundIsRejected() => Invalid("on machine.started { for x in range(0,1.5) {} }", "OS2501");
    [Fact] public void RangeReversalIsRejected() => Invalid("on machine.started { for x in range(5,2) {} }", "OS2501");
    [Fact] public void EqualRangeIsEmptyAndValid() => Valid("on machine.started { for x in range(2,2) {} }");
    [Fact] public void TypedEmptyObjectListCompiles() => Valid("state balls: List<OasisObjectRef> = []; on machine.started { balls = []; }");
    [Fact] public void FunctionsCompileWithTypedParametersAndReturns() => Valid("fn rack_pose(row: Number, col: Number) -> Vec3 { return vec3(row, col, 0); } on machine.started { let p = rack_pose(0, 1); }");
    [Fact] public void TopLevelInitializersCannotCallFunctionsThatCouldRunHostCommands() => Invalid("fn mutate() -> Number { timer.start(\"x\", 1); return 1; } const n = mutate();", "OS2104");
    [Fact] public void ReturnOutsideFunctionIsRejected() => Assert.False(Compile("on machine.started { return; }").Success);
    [Fact] public void AssertionAndSpatialBuiltinsCompile() => Valid("on machine.started { require(surface.contains_sphere(surface:table, frame.position(anchor:cue, vec3(0,0,0)), collider.sphere_radius(prefab:ball)), \"fits\"); }");

    [Fact] public void PoolSampleCompiles() => Valid(Pool);
    [Fact] public void WhacAMoleSampleCompiles() => Valid(WhacAMole);
    [Fact] public void FruitReferencesCompile() => Valid("const startInput=input:start; const featureLamp=lamp:17; const mainReel=reel:2; const alphaDisplay=alpha:0; const digits=sevenSegment:12;");

    private const string Pool = @"const rackBalls=[oasis:ball01,oasis:ball02,oasis:ball03]; const rackAnchors=[anchor:rackBall01,anchor:rackBall02,anchor:rackBall03]; state trayIndex=0;
on trigger.entered(pocket,ball) { if ball==oasis:cueBall { oasis.teleport(ball,anchor:cueBallReturn); oasis.set_velocity(ball,vec3(0,0,0)); oasis.set_angular_velocity(ball,vec3(0,0,0)); } }
on input.pressed(input:rerack) { for i in range(0,3) { oasis.teleport(rackBalls[i],rackAnchors[i]); oasis.set_velocity(rackBalls[i],vec3(0,0,0)); oasis.set_angular_velocity(rackBalls[i],vec3(0,0,0)); } trayIndex=0; }";
    private const string WhacAMole = @"state activeMole=0; const moles=[oasis:mole1,oasis:mole2,oasis:mole3]; const upPositions=[anchor:mole1Up,anchor:mole2Up,anchor:mole3Up]; on machine.started { activeMole=1; oasis.teleport(moles[activeMole],upPositions[activeMole]); timer.start(""hideMole"",1.5); } on timer.elapsed(""hideMole"") { oasis.reset(moles[activeMole]); }";
}
