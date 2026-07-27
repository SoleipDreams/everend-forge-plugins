using System.Runtime.CompilerServices;

// The Editor assembly is part of this package: its importer and projection adapters
// consume the catalog index directly. Game code keeps using IEverendNarrativeGateway.
[assembly: InternalsVisibleTo("EverendForge.Unity.Editor")]
