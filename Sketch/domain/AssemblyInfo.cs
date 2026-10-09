using System.Runtime.CompilerServices;

// From outside the domain only the public anchor SketchDomain is visible; every
// other type is internal. A host reaches the canvas's verbs by reflection over
// the assembly. The test suite is the one trusted insider.
[assembly: InternalsVisibleTo("SketchDomain.Tests")]
