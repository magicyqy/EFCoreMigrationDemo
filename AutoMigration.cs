using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using Microsoft.CodeAnalysis;
using System.Reflection;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using System.Linq;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations.Design;
using Microsoft.EntityFrameworkCore.SqlServer.Design.Internal;
using Microsoft.Extensions.Logging;

namespace EFCoreMigrationDemo
{
    public class AutoMigration
    {
        IHostEnvironment _env;
        string _migrationPath = "Migrations";
        string _connectionString = string.Empty;
        ILogger<AutoMigration> _logger;
        public AutoMigration(IHostEnvironment environment, IConfiguration configuration, ILogger<AutoMigration> logger)
        {
            _logger = logger;
            _env = environment;
            var configration = configuration;
            _connectionString = configration["ConnectionString"];

        }

        /// <summary>
        /// efcore migration操作
        /// </summary>
        /// <param name="ignoreFK">是否忽略外键(即不生成外键)</param>
        public void Migrate(bool ignoreFK = true)
        {
            var basePath = Path.Combine(_env.ContentRootPath, _migrationPath);
            if (!Directory.Exists(basePath))
            {
                Directory.CreateDirectory(basePath);
            }
            var migrationName = $"{DateTimeOffset.Now.ToString("yyyyMMddHHmmss")}_MigrationDemo.Migrations";
            var rootNameSpace = $"EFCoreMigrationDemo.Migrations";
            var projectDir = _env.ContentRootPath;
            //生成之前旧的migration的程序集
            var accemblyName = LoadScaffolderMigrations(basePath, $"Last_MigrationDemo.Migrations", rootNameSpace);
            _logger.LogInformation($"查找并生成上一次migration,{accemblyName}");
            using (var context = CreateDbContext(accemblyName))
            {

                var designTimeServiceCollection = new ServiceCollection()
                    .AddEntityFrameworkDesignTimeServices()
                    .AddDbContextDesignTimeServices(context);
                if (ignoreFK)
                {
                    //不生成外键，覆盖自带的生成类
                    designTimeServiceCollection.AddSingleton<ICSharpSnapshotGenerator, IgnoreFKCSharpSnapshotGenerator>();
                    designTimeServiceCollection.AddSingleton<ICSharpMigrationOperationGenerator, IgnoreFKCSharpMigrationOperationGenerator>();

                }

                //根据数据库类型创建,查找对应的IDesignTimeServices
                new SqlServerDesignTimeServices().ConfigureDesignTimeServices(designTimeServiceCollection);
                var designTimeServicesProvider = designTimeServiceCollection.BuildServiceProvider();
                var scaffolder = designTimeServicesProvider.GetRequiredService<IMigrationsScaffolder>();
                //var denpensies = designTimeServicesProvider.GetService<MigrationsScaffolderDependencies>();
                var csharpMigrationsGenerator = designTimeServicesProvider.GetService<IMigrationsCodeGenerator>();
                var migration = scaffolder.ScaffoldMigration(migrationName, rootNameSpace);
                scaffolder.Save(projectDir, migration, basePath);

                //另一种保存文件方式
                //File.WriteAllText(
                //	Path.Combine(path, migration.MigrationId + migration.FileExtension),
                //	migration.MigrationCode);
                //File.WriteAllText(
                //	Path.Combine(path, migration.MigrationId + ".Designer" + migration.FileExtension),
                //	migration.MetadataCode);
                //File.WriteAllText(
                //	Path.Combine(path, migration.SnapshotName + migration.FileExtension),
                //	migration.SnapshotCode);

            }
            //生成包含新的migration内容的程序集
            accemblyName = LoadScaffolderMigrations(basePath, migrationName, rootNameSpace);
            _logger.LogInformation($"生成新的migration,{accemblyName}");
            using (var context = CreateDbContext(accemblyName))
            {
                context.Database.Migrate();
                context.Database.EnsureCreated();
            }
            _logger.LogInformation($"migration完成");
        }


        private DbContext CreateDbContext(string accemblyName)
        {
            var builder = new DbContextOptionsBuilder<NewDbContext>();
            builder.UseSqlServer(_connectionString, optionBuilder => optionBuilder.MigrationsAssembly(accemblyName));

            return new NewDbContext(builder.Options);
        }

        private string LoadScaffolderMigrations(string outputDir, string migrationName, string rootNameSpace)
        {
            var sourceResult = CreateSyntaxTree(outputDir, migrationName);
            var syntaxTrees = sourceResult.Item1;
            if (syntaxTrees.Length <= 0)
                return null;
            var dotnetCoreDirectory = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();
            var symbolsName = Path.ChangeExtension(migrationName, "pdb");
#if DEBUG
            var optimizationLevel = OptimizationLevel.Debug;
#else
			var optimizationLevel = OptimizationLevel.Release;
#endif
            var references = new List<MetadataReference> {
                    MetadataReference.CreateFromFile(typeof(object).GetTypeInfo().Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(Console).GetTypeInfo().Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(NewDbContext).GetTypeInfo().Assembly.Location),
                    MetadataReference.CreateFromFile(Path.Combine(dotnetCoreDirectory, "mscorlib.dll")),
                    MetadataReference.CreateFromFile(Path.Combine(dotnetCoreDirectory, "netstandard.dll")),
                    MetadataReference.CreateFromFile(Path.Combine(dotnetCoreDirectory, "System.Runtime.dll")),
                    MetadataReference.CreateFromFile(Path.Combine(dotnetCoreDirectory, "System.Linq.Expressions.dll")),

                    MetadataReference.CreateFromFile(Path.Combine(AppContext.BaseDirectory, "Microsoft.EntityFrameworkCore.SqlServer.dll")),
                    MetadataReference.CreateFromFile(Path.Combine(AppContext.BaseDirectory, "Microsoft.EntityFrameworkCore.Relational.dll")),
                    MetadataReference.CreateFromFile(Path.Combine(AppContext.BaseDirectory, "Microsoft.EntityFrameworkCore.Design.dll")),
                    MetadataReference.CreateFromFile(Path.Combine(AppContext.BaseDirectory, "Microsoft.EntityFrameworkCore.dll"))
            };

            var compilation = CSharpCompilation.Create(migrationName)
                .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                .AddReferences(references)
                .AddSyntaxTrees(syntaxTrees)
                .WithOptions(
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                            .WithOptimizationLevel(optimizationLevel)
                            .WithPlatform(Platform.AnyCpu)
                 );


            // Debug output. In case your environment is different it may show some messages.
            foreach (var compilerMessage in compilation.GetDiagnostics())
                Console.WriteLine(compilerMessage);

            //output to dll file and load 可以生成程序集，然后加载到内存
            //var fileName = Path.Combine(outputDir, migrationName + ".dll");
            //var emitResult = compilation.Emit(fileName);
            //if (emitResult.Success)
            //{
            //	var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(fileName));

            //	return assembly.GetName().Name;

            //}

            ///or to memory stream 或者直接生成到内存
            //using (var memoryStream = new MemoryStream())
            //{
            //	var emitResult = compilation.Emit(memoryStream);
            //	if (emitResult.Success)
            //	{
            //		memoryStream.Seek(0, SeekOrigin.Begin);

            //		var assembly = AssemblyLoadContext.Default.LoadFromStream(memoryStream);
            //		return assembly.GetName().Name;
            //	}
            //}
            //return null;
            //生成到内存，并支持debug的版本
            using (var assemblyStream = new MemoryStream())
            using (var symbolsStream = new MemoryStream())
            {
                var emitOptions = new EmitOptions(
                        debugInformationFormat: DebugInformationFormat.PortablePdb,
                        pdbFilePath: symbolsName);

                var embeddedTexts = sourceResult.Item2;

                EmitResult result = compilation.Emit(
                    peStream: assemblyStream,
                    pdbStream: symbolsStream,
                    embeddedTexts: embeddedTexts,
                    options: emitOptions);

                if (!result.Success)
                {
                    var errors = new List<string>();

                    IEnumerable<Diagnostic> failures = result.Diagnostics.Where(diagnostic =>
                        diagnostic.IsWarningAsError ||
                        diagnostic.Severity == DiagnosticSeverity.Error);

                    foreach (Diagnostic diagnostic in failures)
                        errors.Add($"{diagnostic.Id}: {diagnostic.GetMessage()}");

                    throw new Exception(String.Join("\n", errors));
                }



                assemblyStream.Seek(0, SeekOrigin.Begin);
                symbolsStream?.Seek(0, SeekOrigin.Begin);

                var assembly = AssemblyLoadContext.Default.LoadFromStream(assemblyStream, symbolsStream);
                return assembly.GetName().Name;
            }


        }

        private Tuple<SyntaxTree[], List<EmbeddedText>> CreateSyntaxTree(string outputDir, string migrationName)
        {

            string[] sourceFilePaths = Directory.GetFiles(outputDir, "*.cs", SearchOption.TopDirectoryOnly);

            var encoding = Encoding.UTF8;
            var syntaxTreeList = new List<SyntaxTree>();
            var embeddedTexts = new List<EmbeddedText>();

            ///添加一个空类，防止首次生成返回null导致上下文转向查找当前运行程序集
            var emptyTypeText = "namespace _" + Guid.NewGuid().ToString("N") + "{class Class1{}}";
            syntaxTreeList.Add(CSharpSyntaxTree.ParseText(emptyTypeText));
            foreach (var path in sourceFilePaths)
            {
                var text = File.ReadAllText(path);
                var syntaxTree = CSharpSyntaxTree.ParseText(
                    text,
                    new CSharpParseOptions(),
                    path: path);

                var syntaxRootNode = syntaxTree.GetRoot() as CSharpSyntaxNode;
                var encoded = CSharpSyntaxTree.Create(syntaxRootNode, null, path, encoding);
                syntaxTreeList.Add(encoded);
                var buffer = encoding.GetBytes(text);
                embeddedTexts.Add(EmbeddedText.FromSource(path, SourceText.From(buffer, buffer.Length, encoding, canBeEmbedded: true)));


            }
            return Tuple.Create(syntaxTreeList.ToArray(), embeddedTexts);

        }
    }
}
