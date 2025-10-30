namespace ExperienceEdgeEmu.Web.DataStore.Crawler;

[Serializable]
public class GraphQLResponseDataNullException : Exception
{
    public GraphQLResponseDataNullException() : base("GraphQL response data was null.")
    {

    }
}
