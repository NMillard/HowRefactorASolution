using System.Collections.Generic;
using CompositionOverInheritance.Composition;
using DelegatingConfiguration;
using FactoryPattern.Factories;
using FactoryPattern.Factories.FullyOpenClosed;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using RemovingTraditionalBranching;
using RemovingTraditionalBranching.Branchless;

namespace DemoWebApp {
    public class Startup {
        public Startup(IConfiguration configuration) {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services) {
            services.AddControllers();
            services.AddSwaggerGen(c => {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "DemoWebApp", Version = "v1" });
            });

            services.AddSingleton<FactoryPattern.Factories.SwitchBased.ScheduleFactory>();
            services.AddSingleton<FactoryPattern.Factories.DictionaryBased.ScheduleFactory>(_ => {
                var scheduleFactory = new FactoryPattern.Factories.DictionaryBased.ScheduleFactory();
                
                scheduleFactory.RegisterSchedule("Daily", _ => new DailyTrigger());
                scheduleFactory.RegisterSchedule("EndOfWeek", _ => new EndOfWeekTrigger());
                scheduleFactory.RegisterSchedule("Weekly", parameters => {
                    var p = parameters as WeeklyParameters;
                    return new WeeklyTrigger(p.DayOfWeek);
                });
                
                return scheduleFactory;
            });
            
            services.AddSingleton<ScheduleFactoryProvider>();

            services.AddSingleton<FileSystemSavingOptions>();
            services.AddTransient<IFilesRepository, FakeFileRepository>();
            // services.TryAddEnumerable(new List<ServiceDescriptor> {
            //     new(typeof(IFileSink), typeof(DatabaseSink), ServiceLifetime.Transient),
            //     new(typeof(IFileSink), typeof(FileSystemSink), ServiceLifetime.Transient),
            // });
            
            services.TryAddEnumerable(new List<ServiceDescriptor> {
                new (typeof(FileSink<FileSystemContext>), typeof(FileSystemSink), ServiceLifetime.Scoped),
                new (typeof(FileSink<DatabaseFileContext>), typeof(DatabaseSink), ServiceLifetime.Scoped),
            });
            services.AddTransient<RemovingTraditionalBranching.Branchless.FileSaver>(provider => {
                var fileSaver = new RemovingTraditionalBranching.Branchless.FileSaver();
                fileSaver.RegisterSink(provider.GetRequiredService<FileSink<FileSystemContext>>);
                fileSaver.RegisterSink(provider.GetRequiredService<FileSink<DatabaseFileContext>>);
                return fileSaver;
            });
            services.AddTransient<RemovingTraditionalBranching.Traditional.FileSaver>();

            Configuration.BindSimpleSettings(out SomeSettings settings);
            services
                .AddSingleton(settings)
                .AddConfiguredSettings<SomeSettings>(Configuration)
                .AddSomeService(otherSettings => {
                    otherSettings.DefaultFilePath = Configuration["OtherSettings:DefaultFilePath"];
                })
                .AddConfiguration(() => {
                    Configuration.BindSimpleSettings(out SomeSettings someSettings);
                    return someSettings;
                });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env) {
            if (env.IsDevelopment()) {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "DemoWebApp v1"));
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
        }
    }
}