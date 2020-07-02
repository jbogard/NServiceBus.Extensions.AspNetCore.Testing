using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace NServiceBus.Extensions.AspNetCore.Testing.Tests
{
    public class HostApplicationFactoryTests : IClassFixture<HostApplicationFactoryTests.TestFactory>
    {
        private readonly TestFactory _factory;

        public HostApplicationFactoryTests(TestFactory factory) => _factory = factory;

        [Fact]
        public async Task Can_send_and_wait()
        {
            var firstMessage = new FirstMessage {Message = "Hello World"};

            var results = 
                (await _factory.SendLocalAndWaitForHandled<FinalMessage>(firstMessage))
                .ToList();

            results.ShouldNotBeEmpty();

            var message = results.Single();

            message.Message.ShouldBe(firstMessage.Message);
        }

        [Fact]
        public async Task Will_timeout_when_message_never_arrives()
        {
            var firstMessage = new FirstMessage {Message = "Hello World"};

            var results = await _factory.SendLocalAndWaitForHandled<NotHandledMessage>(firstMessage, TimeSpan.FromSeconds(2));

            Should.Throw<TimeoutException>(() => results.ToList());
        }

        public class TestFactory : HostApplicationFactory<HostApplicationFactoryTests>
        {
            protected override IHostBuilder CreateHostBuilder() =>
                Host.CreateDefaultBuilder()
                    .UseNServiceBus(ctxt =>
                    {
                        var endpoint = new EndpointConfiguration("HostApplicationFactoryTests");

                        endpoint.ConfigureTestEndpoint();

                        return endpoint;
                    })
                    .ConfigureWebHostDefaults(b => b.Configure(app => {}));

            protected override IHost CreateHost(IHostBuilder builder)
            {
                builder.UseContentRoot(Directory.GetCurrentDirectory());
                return base.CreateHost(builder);
            }
        }

        public class FirstMessage : ICommand
        {
            public string Message { get; set; }
        }

        public class SecondMessage : ICommand
        {
            public string Message { get; set; }
        }

        public class FinalMessage : ICommand
        {
            public string Message { get; set; }
        }

        public class NotHandledMessage : ICommand
        {
            public string Message { get; set; }
        }

        public class FirstHandler : IHandleMessages<FirstMessage>
        {
            public Task Handle(FirstMessage message, IMessageHandlerContext context) => 
                context.SendLocal(new SecondMessage {Message = message.Message});
        }

        public class SecondHandler : IHandleMessages<SecondMessage>
        {
            public Task Handle(SecondMessage message, IMessageHandlerContext context) => 
                context.SendLocal(new FinalMessage {Message = message.Message});
        }

        public class FinalHandler : IHandleMessages<FinalMessage>
        {
            public Task Handle(FinalMessage message, IMessageHandlerContext context) => 
                Task.CompletedTask;
        }


    }
}
