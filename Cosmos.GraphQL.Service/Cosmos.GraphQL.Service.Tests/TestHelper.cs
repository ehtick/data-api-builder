﻿using Cosmos.GraphQL.Service.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cosmos.GraphQL.Service.Tests
{
    class TestHelper
    {
        public static readonly string DB_NAME = "myDB";
        public static readonly string COL_NAME = "myCol";
        public static readonly string QUERY_NAME = "myQuery";
        public static readonly string MUTATION_NAME = "addPost";
        public static string GraphQLTestSchema = @"
                                        type Query {
                                          myQuery: MyPojo
                                        }

                                        type MyPojo {
                                            myProp : String
                                            id : String
                                        }

                                        type Mutation {
                                            addPost(
                                                myProp: String!
                                                id : String!
                                            ): MyPojo
                                        }";


        public static string SampleQuery = "{\"query\": \"{myQuery { myProp    }}\" } ";

        public static string SampleMutation = "{\"query\": \"mutation addPost {addPost(myProp : \\\"myValueBM \\\"id : \\\"myIdBM \\\") { myProp}}\"}";

        public static GraphQLQueryResolver SampleQueryResolver()
        {
            var raw =
                "{\r\n    \"id\" : \"myQuery\",\r\n    \"databaseName\": \"" + DB_NAME + "\",\r\n    \"containerName\": \"" + COL_NAME + "\",\r\n    \"parametrizedQuery\": \"SELECT * FROM r\"\r\n}";

            return JsonConvert.DeserializeObject<GraphQLQueryResolver>(raw);
        }

        public static MutationResolver SampleMutationResolver()
        {
            var raw =
                "{   \"id\": \"addPost\",\r\n    \"databaseName\": \"" + DB_NAME + "\",\r\n    \"containerName\": \"" + COL_NAME + "\",\r\n    \"operationType\": \"UPSERT\"\r\n}";
            return JsonConvert.DeserializeObject<MutationResolver>(raw);
        }

        public static object GetItem(string id)
        {
            return new
            {
                id = id,
                myProp = "a value",
                myIntProp = 4,
                myBooleanProp = true,
                anotherPojo = new
                {
                    anotherProp = "myname",
                    anotherIntProp = 55,
                    person = new
                    {
                        firstName = "A Person",
                        lastName = "the last name",
                        zipCode = 784298
                    }
                }
            };
        }
    }
}