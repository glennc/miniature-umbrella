using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace api
{
    public class TodoList
    {

        [BsonId]
        public Guid Owner { get; private set; }
        public List<Todo> Todos { get; set; }

        public TodoList(Guid owner)
        {
            Owner = owner;
            Todos = new List<Todo>();
        }
    }

    public class Todo
    {
        [BsonId]
        public string Title { get; set; }
        public bool IsComplete { get; set; }
    }
}
