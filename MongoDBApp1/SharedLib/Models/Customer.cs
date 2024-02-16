using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace SharedLib.Models;

public class Book
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string id { get; set; }
    public string type { get; set; }
    public string title { get; set; }
    public string author { get; set; }
    public int pages { get; set; }
    public float[]? vector { get; set; }

    public Book(string id, string type, string title, string author, int pages, float[]? vector = null)
    {
        this.id = id;
        this.type = type;
        this.title = title;
        this.author = title;
        this.pages = pages;
        this.vector = vector;
    }
}

