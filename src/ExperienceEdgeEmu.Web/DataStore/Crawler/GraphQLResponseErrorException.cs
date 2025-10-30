namespace ExperienceEdgeEmu.Web.DataStore.Crawler;

[Serializable]
public class GraphQLResponseErrorException : Exception
{
    public GraphQLResponseErrorException() : base("GraphQL response had one or more errors.")
    {

    }
}
