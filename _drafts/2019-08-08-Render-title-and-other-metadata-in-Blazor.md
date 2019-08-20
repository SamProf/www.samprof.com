---
title: Заголовок и другие метаданные в Blazor (Client side and Server side)
layout: post
---


Часто нам требуется установить заголовок страницы `<title>` или прочие метаданные `<meta ...>`. Это нужно для удобства пользователя или для поисковой оптимизации.
Однако на первый вгляд это может показаться сложным в Blazor. 
В Client-side Blazor мы обычно имеем статический сайт и не управляем генерацией заголовка.


В Server-side Blazor за генерацию страницы обычно отвечает Razor pages, который на момент генерации заголовка `<title>` в общем случае ничего не знает про рендеринг Blazor - приложения.
Из пожеланий, независимо от модели хостинга Blazor управлять за заголовоком страницы хотелось бы  не только при первоначальной загрузке сатницы, но и при прочих переходах внутри приложения.


Мы будем рассматривать данную проблему на примере только заголовка `<title>`, однако расширить возможности для других метаданных не так уж сложно.


Рещения, которые я предлагаю, немного отличаются для Client и Server Blazor, поэтому мы рассмотрим их по отдальности.

Но есть некоторая общая часть. С неё и начнем.

## PageMetadataService
Для начала нам потребуется сервис-хранилище для заголовка `PageMetadataService`. Так же  `PageMetadataService` должен включать событие об изменении заголовка страницы.

```
public class PageMetadataService
{
    private string _title { get; set; }

    public string Title
    {
        get => _title;
        set
        {
            if (_title != value)
            {
                _title = value;
                OnTitleChanged(value);
            }
        }
    }

    public event EventHandler<string> TitleChanged;


    protected virtual void OnTitleChanged(string e)
    {
        TitleChanged?.Invoke(this, e);
    }
}
```

Так как Blazor приложение (Client и Server) живет внутри одного соединения, то нам нужно зарегистрировать это сервис нужно зарегистрировать с `Scoped` жизненным циклом.

```
public void ConfigureServices(IServiceCollection services)
{
    services.AddScoped<PageMetadataService>();
}
```


## Установка заголовка страницы в Client-Side Blazor
В Client-side Blazor у насть есть возможность ассоциировать Blazor-компонент с DOM-элементом. Обычно в стандартом Blazor приложении регистрируется только одна ассоциация для главного компонента приложения `App`, но нам никто не мешает зарегистрировать дополнительные компоненты. Синтаксис этого метода следующий
```
app.AddComponent<ComponentType>(string domElementSelector);
```
Поэтому мы можем создать компонент `PageMetadataTitle.razor` и зарегистрировать его в приложении с селектром `head>title`, что позволит нам ассоциировать этот компонент с тегом `<title>`.

```
app.AddComponent<PageMetadataTitle>("head>title");
```

При этом, когда Blazor приложение запустилось, оно полностью заменяет контент выбранного DOM-элемента контентом компонента, оставля сам элемент на месте.

По этой причине внутри компонента `PageMetadataTitle` мы должны сгенерировать только заголовок приложения. Данный компонент должен получить зависимость PageMetadataService, и сгенерировать контент компонента Title из этого сервиса. Так же компонент должен подписаться на событие об измении заголовка и вызвать `StateHasChanged()`, что бы мяенять своё содержимое при изменении заголовка.

```
@implements IDisposable
@inject PageMetadataService service

@service.Title

@code
{

    protected override void OnInitialized()
    {
        base.OnInitialized();
        service.TitleChanged += TitleChangedHandler;
    }


    protected void TitleChangedHandler(object sender, string e)
    {
        InvokeAsync(this.StateHasChanged);
    }

    public void Dispose()
    {
        service.TitleChanged -= TitleChangedHandler;
    }

}
```



