---
title: C# and Blazor Compilation inside Browser
date: 2018-12-15 00:00:00 Z
tags:
- blazor
- csharp
- en
- webassembly
layout: post
---

# Introduction

![](/images/compile-blazor-in-browser.png)

If you are a web developer and are developing for a browser, then you are shure know JS, which can be executed inside a browser. There is an opinion that JS is not very suitable for complex calculations and algorithms. And although in recent years JS has made a big breakthrough in performance and wide of use, many developers continue to dream of launching a system language inside the browser. In the near future, the game may change thanks to WebAssembly.

Microsoft is not standing on place and actively trying to port .NET to WebAssembly. As one of the results, we received a new framework for client-side development - Blazor. It is not quite clear yet whether Blazor can be faster than modern JS frameworks like React, Angular, Vue due to WebAssembly. But it definitely has a big advantage - development in C # as well as the whole .NET Core world can be used inside the application.

# Compiling and running C# in a browser
The process of compiling and executing such a complex language as C # is a complex and time-consuming task. `Is it possible to compile and execute C # inside the browser?` However, Microsoft, as it turned out, had already prepared everything for us.

First, let's create a Blazor app.
![Create Blazor Application](/images/2018-12-14.png)

After that, you need to install Nuget package for analyzing and compiling C #.
```
Install-Package Microsoft.CodeAnalysis.CSharp
```

Let's prepare the start page.
```
@page "/"
@inject CompileService service

<h1>Compile and Run C# in Browser</h1>

<div>
    <div class="form-group">
        <label for="exampleFormControlTextarea1">C# Code</label>
        <textarea class="form-control" id="exampleFormControlTextarea1" rows="10" bind="@CsCode"></textarea>
    </div>
    <button type="button" class="btn btn-primary" onclick="@Run">Run</button>
    <div class="card">
        <div class="card-body">
            <pre>@ResultText</pre>
        </div>
    </div>
    <div class="card">
        <div class="card-body">
            <pre>@CompileText</pre>
        </div>
    </div>
</div>

@functions
{
    string CsCode { get; set; }
    string ResultText { get; set; }
    string CompileText { get; set; }

    public async Task Run()
    {
        ResultText = await service.CompileAndRun(CsCode);
        CompileText = string.Join("\r\n", service.CompileLog);
        this.StateHasChanged();
    }
}
```

First you need to parse the string into an abstract syntax tree. Since in the next step we will be compiling the Blazor components, we need the latest (`LanguageVersion.Latest`) version of the language. For this, Roslyn for C # has a method:
```
SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.Latest));
```
Already at this stage, you can detect compilation errors by reading the parser diagnostics.
```
            foreach (var diagnostic in syntaxTree.GetDiagnostics())
            {
                CompileLog.Add(diagnostic.ToString());
            }
```

Next, compile `Assembly` into a memory stream.
```
            CSharpCompilation compilation = CSharpCompilation.Create("CompileBlazorInBlazor.Demo", new[] {syntaxTree},
                references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using (MemoryStream stream = new MemoryStream())
            {
                EmitResult result = compilation.Emit(stream);
            }

```

Note that you need to get the `references` - list of the metadata of the connected Assemblies. But reading these files along the path `Assembly.Location` did not work, because there is no file system in the browser. Perhaps there is a more effective way to solve this problem, but the goal of this article is a conceptual possibility, so we download these libraries again via Http and do it only when we first start compiling.

```
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    references.Add(
                        MetadataReference.CreateFromStream(
                            await this._http.GetStreamAsync("/_framework/_bin/" + assembly.Location)));
                }

```

From `EmitResult` you can find out if the compilation was successful, as well as get diagnostic errors.
Now we need to load `Assembly` into the current` AppDomain` and execute the compiled code. Unfortunately, there is no possibility to create several `AppDomain` inside the browser, so it is safe to load and unload` Assembly`.
```
                Assembly assemby = AppDomain.CurrentDomain.Load(stream.ToArray());
                var type = assemby.GetExportedTypes().FirstOrDefault();
                var methodInfo = type.GetMethod("Run");
                var instance = Activator.CreateInstance(type);
                return (string) methodInfo.Invoke(instance, new object[] {"my UserName", 12});
```


![](/images/2018-12-15.png)
At this stage, we compiled and executed C # code directly in the browser. A program can consist of several files and use other .NET libraries. Is not that great? Now we need to fo deeper.

![](/images/We_need_to_go_deeper.jpg)

# Compiling and running Blazor Components in a browser
Blazor components are modified `Razor` templates. To compile the Blazor component, you need to create a whole environment for compiling Razor templates and set up extensions for Blazor. You need to install the `Microsoft.AspNetCore.Blazor.Build` package from nuget. However, adding it to our Blazor project will not work, because then the linker will not can to compile the project. Therefore, you need to download it, and then manually add 3 libraries.

```
microsoft.aspnetcore.blazor.build\0.7.0\tools\Microsoft.AspNetCore.Blazor.Razor.Extensions.dll
microsoft.aspnetcore.blazor.build\0.7.0\tools\Microsoft.AspNetCore.Razor.Language.dll
microsoft.aspnetcore.blazor.build\0.7.0\tools\Microsoft.CodeAnalysis.Razor.dll
```
Create engine to compile `Razor` and modify it for Blazor, since by default the engine will generate Razor code for the pages.
```
            var engine = RazorProjectEngine.Create(BlazorExtensionInitializer.DefaultConfiguration, fileSystem, b =>
                {
                    BlazorExtensionInitializer.Register(b);                    
                });
```
Only the `fileSystem` is missing for execution - it is an abstraction over the file system. We have implemented an empty file system, however, if you want to compile complex projects with support for `_ViewImports.cshtml`, then you need to implement a more complex structure in memory.
Now we will generate the code from the Blazor component, the C # code.
```
            var file = new MemoryRazorProjectItem(code);
            var doc = engine.Process(file).GetCSharpDocument();
            var csCode = doc.GeneratedCode;
```
From `doc` you can also get diagnostic messages about the results of generating C# code from the Blazor component.
Now we got the code for the C# component. You need to parse `SyntaxTree`, then compile Assembly, load it into the current `AppDomain` and find the `Type` of the component. Same as in the previous example.

It remains to load this component into the current application. There are several ways to do this, for example, by creating your `RenderFragment`.
```
@inject CompileService service

    <div class="card">
        <div class="card-body">
            @Result
        </div>
    </div>

@functions
{
    RenderFragment Result = null;
    string Code { get; set; }    

    public async Task Run()
    {
            var type = await service.CompileBlazor(Code);
            if (type != null)
            {         
                Result = builder =>
                {
                    builder.OpenComponent(0, type);
                    builder.CloseComponent();
                };
            }
            else
            {             
                Result = null;
            }
    }
}

```

![](/images/2018-12-15-2.png)


# Conclusion

We compiled and launched the Blazor component in the browser. Obviously, a full compilation of dynamic C# code right inside the browser can impress any developer.
But here it is necessary to take into account such "pitfalls":
- To support two-way bindings, `bind` needs additional extensions and libraries.
- To support `async, await`, similarly connect extension libraries
- Compiling nested Blazor components will require a two-step compilation.
All these problems have already been solved and this is a topic for a separate article.

GIT: [https://github.com/BlazorComponents/CompileBlazorInBlazor](https://github.com/BlazorComponents/CompileBlazorInBlazor)

Demo: [https://blazorcomponents.github.io/CompileBlazorInBlazor/](https://blazorcomponents.github.io/CompileBlazorInBlazor/)
