namespace TrackSystem.Api.Tests.Infrastructure;

// collection "Api" -> ONE TestApiFactory for every class marked [Collection("Api")]
// classes in one collection run one after another -> never two at the same time on the test DB
[CollectionDefinition("Api")]
public class ApiCollection : ICollectionFixture<TestApiFactory>
{
    // empty on purpose: the class only carries the label above
}