using System.Linq;
using System.Threading.Tasks;
using Castle.MicroKernel.Resolvers.SpecializedResolvers;
using MediatR.Pipeline;

namespace MediatR.Examples.Windsor;

using System;
using System.Collections.Generic;
using System.IO;
using Castle.MicroKernel.Registration;
using Castle.Windsor;

internal class Program
{
    private static Task Main(string[] args)
    {
        var writer = new WrappingWriter(Console.Out);
        var mediator = BuildMediator(writer);

        return Runner.Run(mediator, writer, "Castle.Windsor", true);
    }

    private static IMediator BuildMediator(WrappingWriter writer)
    {
        var container = new WindsorContainer();
        container.Kernel.Resolver.AddSubResolver(new CollectionResolver(container.Kernel));
        container.Kernel.AddHandlersFilter(new ContravariantFilter());

        // *** The default lifestyle for Windsor is Singleton
        // *** If you are using ASP.net, it's better to register your services with 'Per Web Request LifeStyle'.

        var fromAssemblyContainingPing = Classes.FromAssemblyContaining<Ping>();
        container.Register(fromAssemblyContainingPing.BasedOn(typeof(IRequestHandler<,>)).WithServiceAllInterfaces().AllowMultipleMatches());
        container.Register(fromAssemblyContainingPing.BasedOn(typeof(INotificationHandler<>)).WithServiceAllInterfaces().AllowMultipleMatches());
        container.Register(Component.For(typeof(IPipelineBehavior<,>)).ImplementedBy(typeof(RequestExceptionActionProcessorBehavior<,>)));
        container.Register(Component.For(typeof(IPipelineBehavior<,>)).ImplementedBy(typeof(RequestExceptionProcessorBehavior<,>)));
        container.Register(fromAssemblyContainingPing.BasedOn(typeof(IRequestExceptionHandler<,,>)).WithServiceAllInterfaces().AllowMultipleMatches());
        container.Register(fromAssemblyContainingPing.BasedOn(typeof(IStreamRequestHandler<,>)).WithServiceAllInterfaces().AllowMultipleMatches());
        container.Register(fromAssemblyContainingPing.BasedOn(typeof(IRequestPreProcessor<>)).WithServiceAllInterfaces().AllowMultipleMatches());
        container.Register(fromAssemblyContainingPing.BasedOn(typeof(IRequestPostProcessor<,>)).WithServiceAllInterfaces().AllowMultipleMatches());

        container.Register(Component.For<IMediator>().ImplementedBy<Mediator>());
        container.Register(Component.For<TextWriter>().Instance(writer));
        container.Register(Component.For<ServiceFactory>().UsingFactoryMethod<ServiceFactory>(k => (type =>
        {
            var enumerableType = type
                .GetInterfaces()
                .Concat(new[] { type })
                .FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>));

            var service = enumerableType?.GetGenericArguments()?[0];
            var resolvedType = enumerableType != null ? k.ResolveAll(service) : k.Resolve(type);
            var genericArguments = service?.GetGenericArguments();

            // Handle exceptions even using the base request types
            if (service == null
            || genericArguments == null
            || !service.IsInterface
            || !service.IsGenericType
            || !service.IsConstructedGenericType
            || !(service.GetGenericTypeDefinition()
            ?.IsAssignableTo(typeof(IRequestExceptionHandler<,,>)) ?? false)
            || genericArguments.Length != 3
            || !(genericArguments[0].BaseType?.IsClass ?? false))
            {
                return resolvedType;
            }
            
            var serviceFactory = k.Resolve<ServiceFactory>();
            var baseRequestType = genericArguments[0].BaseType;
            var response = genericArguments[1];
            var exceptionType = genericArguments[2];
            
            // Check if the base request type is valid
            if (!baseRequestType.IsClass
            || baseRequestType == typeof(object)
            || ((!baseRequestType.GetInterfaces()
                ?.Any(i => i.IsAssignableFrom(typeof(IRequest<>)))) ?? true))
            {
                return resolvedType;
            }

            var exceptionHandlerInterfaceType = typeof(IRequestExceptionHandler<,,>).MakeGenericType(baseRequestType, response, exceptionType);
            var enumerableExceptionHandlerInterfaceType = typeof(IEnumerable<>).MakeGenericType(exceptionHandlerInterfaceType);

            // This is assumed Array because this method calls ResolveAll when a IEnumerable<> is passed as argument
            var firstArray = serviceFactory.Invoke(enumerableExceptionHandlerInterfaceType) as Array;
            var secondArray = resolvedType is Array ? resolvedType as Array : new[] { resolvedType };
            var resultArray = Array.CreateInstance(typeof(object), firstArray.Length + secondArray.Length);
            Array.Copy(firstArray, resultArray, firstArray.Length);
            Array.Copy(secondArray, 0, resultArray, firstArray.Length, secondArray.Length);
            
            return resultArray;
        })));

        //Pipeline
        container.Register(Component.For(typeof(IStreamPipelineBehavior<,>)).ImplementedBy(typeof(GenericStreamPipelineBehavior<,>)));
        container.Register(Component.For(typeof(IPipelineBehavior<,>)).ImplementedBy(typeof(RequestPreProcessorBehavior<,>)).NamedAutomatically("PreProcessorBehavior"));
        container.Register(Component.For(typeof(IPipelineBehavior<,>)).ImplementedBy(typeof(RequestPostProcessorBehavior<,>)).NamedAutomatically("PostProcessorBehavior"));
        container.Register(Component.For(typeof(IPipelineBehavior<,>)).ImplementedBy(typeof(GenericPipelineBehavior<,>)).NamedAutomatically("Pipeline"));
        container.Register(Component.For(typeof(IRequestPreProcessor<>)).ImplementedBy(typeof(GenericRequestPreProcessor<>)).NamedAutomatically("PreProcessor"));
        container.Register(Component.For(typeof(IRequestPostProcessor<,>)).ImplementedBy(typeof(GenericRequestPostProcessor<,>)).NamedAutomatically("PostProcessor"));
        container.Register(Component.For(typeof(IRequestPostProcessor<,>), typeof(ConstrainedRequestPostProcessor<,>)).NamedAutomatically("ConstrainedRequestPostProcessor"));
        container.Register(Component.For(typeof(INotificationHandler<>), typeof(ConstrainedPingedHandler<>)).NamedAutomatically("ConstrainedPingedHandler"));

        var mediator = container.Resolve<IMediator>();

        return mediator;
    }
}
