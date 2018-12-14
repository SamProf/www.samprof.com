---
title: Компиляция и запуск C# и Blazor внутри браузера
tags:
- blazor
- csharp
- ru
- webassembly
layout: post
---

# Введение

![](/images/We_need_to_go_deeper.jpg)

Если вы Web-разработчик и ведете разработку для браузера, то вы точно знакомы с JS, который может исполняться внутри браузера. По мнениею многих JS не сильно подходит для каких-то сложных вычислений и сложных алгоритмов. Хотя последние годы JS cделал большой прыжок в производительности и широте использования, многие системные программисты мечтали запустить какойто системный язык внутри браузера. Но в блидайшее время игра может поменяться благодаря WebAssembly.

Microsoft не стоит на месте и активно пытается портировать .NET в WebAssembly. Как один из результатов мы получили новый фреймворк для клиенской разработки - Blazor. Пока не совсем очевидны, сможет ли данный Blazor за счет WebAssembly быть быстрее современных JS - фреймворков типа React, Angular, Vue. Но он точно имеет большое преимущество - разработка на C#, а так же весь мир .NET Core может быть использован внутри твоего приложения. Разве это не чудесно?

# Компиляция и выполение C#
Процесс компиляции и выполнения такого сложного языка как C#, конечно сложная и трудоемка задача. И вопрос - А можно ли внутри браузера скомпилировать и выполнить С# - вопрос зрелости технологии (а точнее ядра). Однако Microsoft, как оказалось уже все подготовила для нас.

Для начала создадим Blazor приложение.
![Create Blazor Application](/images/2018-12-14.png)

После этого вам нужно установить Nuget - пакет для анализа и компиляции C#.
```
Install-Package Microsoft.CodeAnalysis.CSharp
```

Подготовим стартовую страницу.
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

Для начала нам надо распарсить строку в асбтрактное синтасическое дерево. Так как в следующем этапе мы будем компилировать Blazor компоненты - нам нужен самая последняя (`LanguageVersion.Latest`) версия языка. Для этого Roslyn для C# есть метод:
```
SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.Latest));
```
Уже на этом этапе мы уже можем обнаружить грубые ошибки компиляции, вычитав диагностику парсера.
```
            foreach (var diagnostic in syntaxTree.GetDiagnostics())
            {
                CompileLog.Add(diagnostic.ToString());
            }
```

И сразу мы можем быть готовы к тому, что бы выполнить компиляцию в сборку в бинарный поток.
```
            CSharpCompilation compilation = CSharpCompilation.Create("CompileBlazorInBlazor.Demo", new[] {syntaxTree},
                references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using (MemoryStream stream = new MemoryStream())
            {
                EmitResult result = compilation.Emit(stream);
            }

```

Проблема тут может быть в том, что нужно получить `references` - список метаданных подключенных библиотек. Попробывать прочитать файлы уже подключенных по пути `Assembly.Location` не получилось, так мы в браузере и файловой системы тут нет. Я уверен, что есть более эффективный способ решения этой проблемы, но цель этой статьи концептуальная возможность. Поэтому я решиил, а почему бы не вычитать эти библиотки снова по Http и сделать это толко при первом запуске компиляции?

```
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    references.Add(
                        MetadataReference.CreateFromStream(
                            await this._http.GetStreamAsync("/_framework/_bin/" + assembly.Location)));
                }

```

Из `EmitResult` мы можем узнать была ли успешной компиляция, а так же диагностические ошибки.
Дале, все что нам нужно - это загрузить `Assembly` в текущий `AppDomain` и выполнить скомпилированный нами код. К сожалению внутри браузера у нас нет возможности создавать несколько `AppDomain`, поэтому безопасно загрузить и выгрузить эту `Assembly` у нас не получится.
```
                Assembly assemby = AppDomain.CurrentDomain.Load(stream.ToArray());
                var type = assemby.GetExportedTypes().FirstOrDefault();
                var methodInfo = type.GetMethod("Run");
                var instance = Activator.CreateInstance(type);
                return (string) methodInfo.Invoke(instance, new object[] {"my UserName", 12});
```

И все! На данном этапе мы скомпилировали и выполнили C# код прямо в браузере. Наша программа может быть сколь-угодно сложная, состоять из нескольких файлов и использовать другие .NET библиотеки. Разве это не здорово? Но я хочу пойти дальше - нам нужно идти глубже.

# Компиляция и запуск Blazor компонента в браузере.
На самом деле компоненты Blazor - это модифицированные `Razor` шаблоны. Поэтому для того, что бы скомпилировать Blazor комопнент, нам нужно развернуть целую среду для компиляции Razor шаблонов и настроив дополнение для Blazor. На самом деле для того, что бы скомпилировать Blazor нам нужно установить пакет `Microsoft.AspNetCore.Blazor.Build` из nuget. Однако, добавить его в наш проект Blazor не получиться, так как потом линкер не сможет скомпилировать наш проект. Поэтому нужно его скачать, а потом руками добавить 3 библиотеки.
```
microsoft.aspnetcore.blazor.build\0.7.0\tools\Microsoft.AspNetCore.Blazor.Razor.Extensions.dll
microsoft.aspnetcore.blazor.build\0.7.0\tools\Microsoft.AspNetCore.Razor.Language.dll
microsoft.aspnetcore.blazor.build\0.7.0\tools\Microsoft.CodeAnalysis.Razor.dll
```
Создадим ядро для компиляции `Razor` и модифицируем его для Blazor, так как по умолчанию ядро будет генерировать код Razor страниц.
```
            var engine = RazorProjectEngine.Create(BlazorExtensionInitializer.DefaultConfiguration, fileSystem, b =>
                {
                    BlazorExtensionInitializer.Register(b);                    
                });
```
Для выполненния не хвтает только `fileSystem` - это такая асбтракция над файловой системой. Я реализовал пустую файловую систему, однако, если вы хотите компилировать сложные проекты с поддержкой `_ViewImports.cshtml` - то нужно просто реализовать более чуть более сложную структуру в памяти. 
Теперь сгенерируем код из Blazor компонента C# код.
```
            var file = new MemoryRazorProjectItem(code);
            var doc = engine.Process(file).GetCSharpDocument();
            var csCode = doc.GeneratedCode;
```
Из `doc` можно так же получить диагностику а результатах генерации C# код из Blazor комопнента.
Теперь мы получили код C# компонента, который можно скомпилировать так же, как и в предыдущем примере. Нужно распарсить `SyntaxTree`, потом скомпилировать Assembly, загрузить её в текущий AppDomain и найти тип комопнента. Все это мы уже сделали в предыдущем примере.

Все, что осталось, это загузить этот комопнент, в текущее приложение. Есть несколько способов как это сделать, но мне нравится - создав свой `RenderFragment`.
```
@page "/blazor"
@inject CompileService service
<h1>Compile and Run Blazor in Browser</h1>

<div>
    <div class="form-group">
        <label for="exampleFormControlTextarea1">Blazor Code</label>
        <textarea class="form-control" id="exampleFormControlTextarea1" rows="10" bind="@Code"></textarea>
    </div>
    <button type="button" class="btn btn-primary" onclick="@Run">Run</button>
    <div class="card">
        <div class="card-body">
            @Result
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
    RenderFragment Result = null;
    string Code { get; set; }
    string CompileText { get; set; }

    public async Task Run()
    {
        try
        {
            service.CompileLog = new List<string>();
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
        finally
        {
            CompileText = string.Join("\r\n", service.CompileLog);
            this.StateHasChanged();
        }
    }

}

```

