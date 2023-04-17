using MessageBroker.Data;
using MessageBroker.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(opt =>
{
    opt.UseSqlite("Data Source=MessageBroker.db");
});

var app = builder.Build();

// Create Topic
app.MapPost("api/topics", async (AppDbContext context, Topic topic) =>
{
    await context.Topics.AddAsync(topic);
    await context.SaveChangesAsync();
    return Results.Created($"api/topics/{topic.Id}", topic);
});

// Get All Topics
app.MapGet("api/topics", async (AppDbContext context) =>
{
    var topics = await context.Topics.ToListAsync();
    return Results.Ok(topics);
});

// Publish Message
app.MapPost("api/topics/{id}/messages", async (AppDbContext context, int id, Message message) =>
{
    bool topics = await context.Topics.AnyAsync(t => t.Id == id);
    if (!topics)
    {
        return Results.NotFound("Topic not found.");
    }

    var subscriptions = context.Subscriptions.Where(s => s.TopicId == id);
    if (!subscriptions.Any())
    {
        return Results.NotFound("There are no subscriptions for this topic.");
    }

    foreach(var subscription in subscriptions)
    {
        Message msg = new()
        {
            TopicMessage = message.TopicMessage,
            SubscriptionId = subscription.Id,
            ExpiresAfter = message.ExpiresAfter,
            MessageStatus = message.MessageStatus,
        };
        await context.Messages.AddAsync(msg);
    }

    await context.SaveChangesAsync();

    return Results.Ok("Message has been published.");
});

// Create Subscription
app.MapPost("api/topics/{id}/subscriptions", async (AppDbContext context, int id, Subscription subscription) =>
{
    bool topics = await context.Topics.AnyAsync(t => t.Id == id);
    if (!topics)
    {
        return Results.NotFound("Topic not found.");
    }

    subscription.TopicId = id;

    await context.Subscriptions.AddAsync(subscription);
    await context.SaveChangesAsync();

    return Results.Created($"api/topics/{id}/subscriptions/{subscription.Id}", subscription);
});

// Get Subscriber Messages
app.MapGet("api/subscriptions/{id}/messages", async (AppDbContext context, int id) =>
{
    bool subscriptions = await context.Subscriptions.AnyAsync(t => t.Id == id);
    if (!subscriptions)
    {
        return Results.NotFound("Subscription not found.");
    }

    var messages = context.Messages.Where(m => m.SubscriptionId == id && m.MessageStatus != "SENT");
    if (!messages.Any())
    {
        return Results.NotFound("No new messages found.");
    }

    foreach(var message in messages)
    {
        message.MessageStatus = "REQUESTED";
    }

    await context.SaveChangesAsync();

    return Results.Ok(messages);
});

// Acknowledge Messages for Subscriber
app.MapPost("api/subscriptions/{id}/messages", async (AppDbContext context, int id, int[] confirmations) =>
{
    bool subscriptions = await context.Subscriptions.AnyAsync(t => t.Id == id);
    if (!subscriptions)
    {
        return Results.NotFound("Subscription not found.");
    }

    if (confirmations.Length <= 0)
    {
        return Results.BadRequest("Confirmations must be supplied.");
    }

    int count = 0;
    foreach (int i in confirmations)
    {
        var message = context.Messages.FirstOrDefault(m => m.Id == id);
        if (message != null)
        {
            message.MessageStatus = "SENT";
            await context.SaveChangesAsync();
            count++;
        }
    }

    return Results.Ok($"Acknowledged {count}/{confirmations.Length} messages.");
});

app.UseHttpsRedirection();

app.Run();