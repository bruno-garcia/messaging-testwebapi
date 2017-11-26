using System;
using System.Threading;
using System.Threading.Tasks;
using Greentube.Messaging;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace MessagingTestWebApi
{
    public class Program
    {
        public static void Main(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
            .UseStartup<Startup>()
            .Build()
            .Run();
    }

    public class SomeMessage
    {
        public string Body { get; set; }
    }

    [Route("some-message")]
    public class SomeMessageController : Controller
    {
        private readonly IMessagePublisher _publisher;
        public SomeMessageController(IMessagePublisher publisher)
            => _publisher = publisher;

        [HttpPut]
        public Task PublishSomeMessage([FromBody] SomeMessage message, CancellationToken token)
            => _publisher.Publish(message, token);

        [HttpGet]
        public Task GetNothing(CancellationToken token)
            => _publisher.Publish(new SomeMessage { Body = "Get was called" }, token);
    }

    public class SomeMessageHandler : IMessageHandler<SomeMessage>
    {
        public Task Handle(SomeMessage message, CancellationToken _)
        {
            Console.WriteLine($"Handled: {message}.");
            return Task.CompletedTask;
        }
    }
   
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();
            services.AddMessaging(builder => builder
                .AddRedis(ConnectionMultiplexer.Connect("localhost:6379"))
                .AddSerialization(b => b.AddJson())
                .AddTopic<SomeMessage>("topic"));
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            // TODO: app.UseMessagingSubscriptions()
            var serviceProvider = app.ApplicationServices;
            var map = serviceProvider.GetRequiredService<IMessageTypeTopicMap>();
            var rawSubscriber = serviceProvider.GetRequiredService<IRawMessageHandlerSubscriber>();
            var rawHandler = serviceProvider.GetRequiredService<IRawMessageHandler>();
            foreach (var topic in map.GetTopics())
            {
                rawSubscriber.Subscribe(topic, rawHandler, CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
            }

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseMvc();
        }
    }
}
