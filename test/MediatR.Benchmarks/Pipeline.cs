using System.IO;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using MediatR.Examples;
using Microsoft.Extensions.DependencyInjection;

namespace MediatR.Benchmarks
{
    [MemoryDiagnoser]
    public class Pipeline
    {
        [Params(0, 1, 2, 5, 10, 100, 1000)]
        public int numOfBehavior;

        private IMediator _mediator;
        private readonly Ping _request = new Ping { Message = "Hello World" };

        [GlobalSetup]
        public void GlobalSetup()
        {
            var services = new ServiceCollection();

            services.AddSingleton(TextWriter.Null);

            services.AddMediatR(typeof(Ping));

            for (int i = 0; i < numOfBehavior; i++)
            {
                services.AddScoped(typeof(IPipelineBehavior<,>), typeof(GenericPipelineBehavior<,>));
            }

            var provider = services.BuildServiceProvider();

            _mediator = provider.GetRequiredService<IMediator>();
        }

        [Benchmark]
        public Task SendingRequests()
        {
            return _mediator.Send(_request);
        }
    }
}
