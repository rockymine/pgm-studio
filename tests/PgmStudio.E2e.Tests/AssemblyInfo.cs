// Every spec drives the one server and database tools/e2e.sh brings up, so no two run at once; each spec's
// own [NotInParallel(Order = …)] fixes the order they run in.
[assembly: NotInParallel]
