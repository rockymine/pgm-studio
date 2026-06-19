// TUnit's implicit global usings, declared explicitly so the [Test] attribute and the assertion API
// resolve on a clean first build. TUnit's own injection is disabled in Directory.Build.props (it runs in
// a late .targets the incremental build folds in unreliably); this file is shared by every test project
// via the Compile item there.
global using TUnit.Core;
global using TUnit.Assertions;
global using TUnit.Assertions.Extensions;
global using static TUnit.Core.HookType;
