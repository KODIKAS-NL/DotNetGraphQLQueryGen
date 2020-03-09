# dotnet GraphQL Query generator

Given a GraphQL schema file, this tool will generate interfaces and classes to enable strongly typed querying from C# to a GraphQL API.

Example, given the following GraphQL schema
```
schema {
    query: Query
    mutation: Mutation
}

scalar Date

type Query {
]	actors: [Person]
	directors: [Person]
	movie(id: Int!): Movie
	movies: [Movie]
}

type Movie {
	id: Int
	name: String
	genre: Int
	released: Date
	actors: [Person]
	writers: [Person]
	director: Person
	rating: Float
}

type Person {
	id: Int
	dob: Date
	actorIn: [Movie]
	writerOf: [Movie]
	directorOf: [Movie]
	died: Date
	age: Int
	name: String
}

type Mutation {
    addPerson(dob: Date!, name: String!): Person
}
```

Running `dotnet run -- schema.graphql -m Date=DateTime` will generate the following

```c#
public interface RootQuery
{
    [GqlFieldName("actors")]
    List<Person> Actors();

    [GqlFieldName("actors")]
    List<TReturn> Actors<TReturn>(Expression<Func<Person, TReturn>> selection);

    [GqlFieldName("directors")]
    List<Person> Directors();

    [GqlFieldName("directors")]
    List<TReturn> Directors<TReturn>(Expression<Func<Person, TReturn>> selection);

    [GqlFieldName("movie")]
    Movie Movie();

    [GqlFieldName("movie")]
    TReturn Movie<TReturn>(int id, Expression<Func<Movie, TReturn>> selection);

    [GqlFieldName("movies")]
    List<Movie> Movies();

    [GqlFieldName("movies")]
    List<TReturn> Movies<TReturn>(Expression<Func<Movie, TReturn>> selection);
}

public interface Movie
{
    [GqlFieldName("id")]
    int Id { get; }
    [GqlFieldName("name")]
    string Name { get; }
    [GqlFieldName("genre")]
    int Genre { get; }
    [GqlFieldName("released")]
    DateTime Released { get; }
    [GqlFieldName("actors")]
    List<Person> Actors();
    [GqlFieldName("actors")]
    List<TReturn> Actors<TReturn>(Expression<Func<Person, TReturn>> selection);
    [GqlFieldName("writers")]
    List<Person> Writers();
    [GqlFieldName("writers")]
    List<TReturn> Writers<TReturn>(Expression<Func<Person, TReturn>> selection);
    [GqlFieldName("director")]
    Person Director();
    [GqlFieldName("director")]
    TReturn Director<TReturn>(Expression<Func<Person, TReturn>> selection);
    [GqlFieldName("rating")]
    double Rating { get; }
}

public interface Person
{
    [GqlFieldName("id")]
    int Id { get; }
    [GqlFieldName("dob")]
    DateTime Dob { get; }
    [GqlFieldName("actorIn")]
    List<Movie> ActorIn();
    [GqlFieldName("actorIn")]
    List<TReturn> ActorIn<TReturn>(Expression<Func<Movie, TReturn>> selection);
    [GqlFieldName("writerOf")]
    List<Movie> WriterOf();
    [GqlFieldName("writerOf")]
    List<TReturn> WriterOf<TReturn>(Expression<Func<Movie, TReturn>> selection);
    [GqlFieldName("directorOf")]
    List<Movie> DirectorOf();
    [GqlFieldName("directorOf")]
    List<TReturn> DirectorOf<TReturn>(Expression<Func<Movie, TReturn>> selection);
    [GqlFieldName("died")]
    DateTime Died { get; }
    [GqlFieldName("age")]
    int Age { get; }
    [GqlFieldName("name")]
    string Name { get; }
}

public interface Mutation
{
    [GqlFieldName("addPerson")]
    TReturn AddPerson<TReturn>(DateTime dob, string name, Expression<Func<Person, TReturn>> selection);
}
```

It also generates a `GraphQLClient` class that will work for an unauthenticated API, but it is also possible to use authentication in the following way:
```c#
var httpClient = new HttpClient();
httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "###tokenstring###");
var graphQLClient = new GraphQLClient(new Uri("https://api.url.com/"), httpClient);
```
`GraphQLClient` exposes 2 methods, `async Task<GqlResult<TQuery>> QueryAsync<TQuery>(Expression<Func<RootQuery, TQuery>> query)` for queries and `async Task<GqlResult<TQuery>> MutateAsync<TQuery>(Expression<Func<Mutation, TQuery>> query)` for mutations.

Example usage

```c#
var client = new GraphQLClient();
var result = await client.QueryAsync(q => new {
    Movie = q.Movie(2, m => new {
        m.Id,
        m.Name,
        ReleaseDate = m.Released,
        Director = m.Director(d => new {
            d.Name
        })
    }),
    Actors = q.Actors(a => new {
        a.Name,
        a.Dob
    })
});
```

This looks similar to the GraphQL. And now `result.Data` is a strongly type object with the expected types.

If a field in the schema is an object (can have a selection protected on to it) it will be exposed in the generated code as a method where you pass in any field arguments first and then the selection.

The GraphQL created by the above will look like this
```
{
    query {
        Movie: movie(id: 2) {
            Id: id
            Name: name
            ReleaseDate: released
            Director: director {
                Name: name
            }
        }
        Actors: actors {
            Name: name
            Dob: dob
        }
    }
}
```

## Mutations

Mutations look very similar to the above query, just as they do in GraphQL. The above schema had one mutation that can be called like so

```c#
// mutations
var mutationResult = await client.MutateAsync(m => new {
    NewPerson = m.AddPerson(new DateTime(1801, 7, 3), "John Johny", p => new { p.Id })
});
```

`m` is the `Mutation` type interface and lets you call any of the mutations generated from the schema. Like a query, you create a new anaoymous object and call as many mutations as you wish. Each mutation method has all their arguments and the last one is a selection query on the mutation's return type.

The above has `mutationResult` strongly typed. E.g. `mutationResult.Data.NewPerson` will only have an `Id` field.

## Input types

Any input types are generated as actual `class`es as the are used as arguments. The `-m Date=DateTime` tells the tool that the `scalar` type `Date` should be the `DateTime` type in C#. You can use this to support any other custom `scalar` types. E.g. `-m Date=DateTime,Point=PointF`, just comma seperate a `GqlScalar=DotnetType` list.
