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

<form>
    <div class="form-group">
        <label for="exampleFormControlTextarea1">C# Code</label>
        <textarea class="form-control" id="exampleFormControlTextarea1" rows="10" bind="@CsCode"></textarea>
    </div>
    <button type="button" class="btn btn-primary" onclick="@Run">Run</button>
    <div>
        @ResultText
    </div>
    <div>
        @CompileText
    </div>
</form>

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

Для начала нам надо распарсить строку а асбтрактное синтасическое дерево, для этого Roslyn для C# есть метод:
```
SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(code);
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

Проблема тут может быть в том, что нужно получить `references` - список метаданных подключенных библиотек. Попробывать прочитать файлы уже подключенных по пути `Assembly.Location` не получилось, так мы в браузере и файловой системы тут нет. Я уверен, что есть более эффективный способ решения этой проблемы, но цель этой статьи концептуальная возможность. Поэтому я решиил, а почему бы не вычитать это библиотки снова по Http и сделать это толко при первом запуске компиляции?

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

И все! На данном этапе мы скомпилировали и выполнили C# код прямо в браузере.
