using Snipit.SelfTest;

// Dependency-free logic tests. Run: dotnet run --project tests/Snipit.SelfTest
// Add a new area as Tests.<Area>.cs with one static method and list it here.
Tests.Geometry();
Tests.Settings();
Tests.Encoders();
Tests.Imaging();
Tests.Layout();
Tests.Media();
Tests.Hardening();
Tests.Effects();
Console.WriteLine($"\n{Tests.Passed} passed, {Tests.Failed} failed");
return Tests.Failed == 0 ? 0 : 1;
