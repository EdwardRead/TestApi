
using System.Collections.Concurrent;

namespace TestApi;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddAuthorization();

        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();

        app.UseAuthorization();

        ConcurrentQueue<IImageService> imageServices = [];
        
        app.MapGet("/services", () => imageServices);

        // Service validation should probably be added
        app.MapPost("/services", (IImageService imageService) => 
        {
            imageServices.Enqueue(imageService);
        });

        app.MapPost("/process", async (IProcessQuery query, IImage image) => 
        {
            IImageService? dequeuedService = null; 

            bool noServices = false;
            while ((noServices = imageServices.TryDequeue(out dequeuedService)) && !await query.Matches(dequeuedService!))
            {
                if (noServices)
                {
                    return Results.Problem();
                }
                // There is definitely a better way of representing this but this is just example code so that does not matter. 
                imageServices.Enqueue(dequeuedService!);
            }

            if (dequeuedService is null)
            {
                return Results.Problem();
            }

            // Add timeout support or delegate process call to client. 
            IImage? processed = await dequeuedService.Process(query, image);

            imageServices.Enqueue(dequeuedService);

            return Results.Ok(processed);
        });

        app.Run();
    }
}

public interface IImage
{
}

public interface IProcessQuery
{
    Task<bool> Matches(IImageService imageService);
}

// Because interface is used, local services AND remote services could be added. 
public interface IImageService
{
    Task<IImage?> Process(IProcessQuery query, IImage image);
}
