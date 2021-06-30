using System;
using System.Collections.Generic;
using api;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Identity.Web;
using Microsoft.OpenApi.Models;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Todo", Version = "v1" });
    c.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.OAuth2,
        Flows = new OpenApiOAuthFlows
        {

            AuthorizationCode = new OpenApiOAuthFlow
            {
                AuthorizationUrl = new Uri(builder.Configuration["AuthUri"]),
                TokenUrl = new Uri(builder.Configuration["TokenUri"]),
                Scopes = new Dictionary<string, string>
                            {
                                { "api://d860d34a-10c0-4979-9bf9-9ddca1059fb8/access_as_user", "Access the API" }
                            }
            }
        }
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "oauth2" }
            },
            new[] {"user.read", "api://d860d34a-10c0-4979-9bf9-9ddca1059fb8/access_as_user" }
        }
    });
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

builder.Services.AddAuthorization(o => o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

// Allowing CORS for all domains and methods for the purpose of sample
builder.Services.AddCors(o => o.AddPolicy("default", options =>
{
    options.AllowAnyOrigin()
           .AllowAnyMethod()
           .AllowAnyHeader();
}));

var app = builder.Build();

var client = new MongoClient(builder.Configuration["ConnectionString"]);
var database = client.GetDatabase(builder.Configuration["DatabaseName"]);
var collection = database.GetCollection<TodoList>(builder.Configuration["CollectionName"]);

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "swaggerAuthWithAAD v1");
        c.OAuthClientId(builder.Configuration["AzureAd:ClientId"]);
        c.OAuthClientSecret(builder.Configuration["SwaggerSecret"]);
        c.OAuthUsePkce();
    });
}

app.UseCors("default");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapPost("/addtodo", async (HttpContext ctx, Todo item) =>
{
    var filter = Builders<TodoList>.Filter.Eq(x => x.Owner, Guid.Parse(ctx.User.GetObjectId()));
    var update = Builders<TodoList>.Update.Push(x => x.Todos, item);

    var result = await collection.UpdateOneAsync(filter, update);
}).RequireAuthorization();

app.MapPost("/deletetodo", async (HttpContext ctx, Todo item) =>
{
    var filter = Builders<TodoList>.Filter.Eq(x => x.Owner, Guid.Parse(ctx.User.GetObjectId()));
    var update = Builders<TodoList>.Update.PullFilter(x => x.Todos, y => y.Title == item.Title);

    var result = await collection.UpdateOneAsync(filter, update);
});


app.MapPost("/updatecomplete", async (HttpContext ctx, Todo item) =>
{
    var ownerFilter = Builders<TodoList>.Filter.Eq(x => x.Owner, Guid.Parse(ctx.User.GetObjectId()));
    var itemFilter = Builders<TodoList>.Filter.ElemMatch(x => x.Todos, y => y.Title == item.Title);
    var update = Builders<TodoList>.Update.Set(x => x.Todos[-1].IsComplete, item.IsComplete);

    var result = await collection.UpdateOneAsync(Builders<TodoList>.Filter.And(ownerFilter, itemFilter), update);
});

app.MapGet("/", [Authorize] async (HttpContext ctx) =>
{
    var userId = Guid.Parse(ctx.User.GetObjectId());
    var todoList = await collection.Find(x => x.Owner == userId).SingleOrDefaultAsync();

    if (todoList == null)
    {
        todoList = new TodoList(userId);
        await collection.InsertOneAsync(todoList);
    }

    return todoList?.Todos;
});

app.Run();