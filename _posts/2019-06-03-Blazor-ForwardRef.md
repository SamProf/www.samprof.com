---
title: Forwarding Refs in Blazor
date: 2019-06-03 00:00:00 Z
layout: post
---

ForwardRef is a technique for automatically passing a ElementRef through a component to one of its children or back from children to parent or among independent components. 
This is typically not necessary for most components in the application. However, it can be useful for some kinds of components, especially in reusable component libraries. The most common scenarios are described below.


#### Capture references to elements
From Blazor documentation we know that you can capture references to HTML elements in a component using the following approach:

- Add a `ref` attribute to the HTML element.
- Define a field of type `ElementRef` whose name matches the value of the `ref` attribute.
The following example shows capturing a reference to the `username` `<input>` element: 
```html
<input ref="username" ... />

@functions {
    ElementRef username;
}
```


### Problematics

### Problem in passing a ElementRef to its children or another component
If you try to pass the `ElementRef` to another component (children or neighbor), it will not work. 
Because `ref` returns the value at the Render moment after the parameters have been applied.
The value can be applied after the following `StateHasChanged()`.
```html
<MyTooltipComponent targetRef="@username"></MyTooltipComponent>

<input ref="username" ... />

@functions {
    ElementRef username;
}
```



### Problem in passing a child ElementRef to its parent in Blazor
The same problem is reproduced when you need to get the ElementRef from the ChildContent, especially when ChildContent is not an HTML Element, but Blazor Component.
```html
<MyTooltipComponent Tooltip="My tooltip for h1 element">
    <h1></h1>
</MyTooltipComponent>

<MyTooltipComponent Tooltip="My tooltip for MatBlazor Button">
    <MatButton>Click me</MatButton>
</MyTooltipComponent>
```

## Solution `ForwardRef`
The solution is to create a store for the ElementRef and pass this as parameter to all components.
```
public class ForwardRef
{
    private ElementRef _current;
    
    public ElementRef Current
    {
        get => _current;
        set => Set(value);
    }
    public void Set(ElementRef value)
    {
        _current = value;
    }
}
```

### Solution for passing a ElementRef to its children or another component
When the current component wants to pass its `ElementRef` and pass it on to others, you should create ForwardRef instance and pass it to others components in parameters. 

#### Index.razor
```html
<MyTooltipComponent targetForwardRef="@usernameForwardRef" Tooltip="My tooltip"></MyTooltipComponent>

<input ref="usernameForwardRef.Current" ... />

@functions {
    ForwardRef usernameForwardRef = new ForwardRef();
}
```

#### MyTooltipComponent.razor
```html
<div class="my-tooltip">@Tooltip</div>

@functions {
    [Parameter]
    protected ForwardRef TargetForwardRef {get;set;}
    
    [Parameter]
    protected string Tooltip {get;set;}
    
    protected override async Task OnAfterRenderAsync()
    {
        // TargetForwardRef.Current will contain reference to target ElementRef 
        await js.InvokeAsync<object>("initTooltip", TargetForwardRef.Current);
    }
}
```



### Solution in passing a child ElementRef to its parent in Blazor
If you want to get a reference to a `ElementRef` of child, you should pass the `ForwardRef` to the child in which the child returns `ElementRef` to itself. 

#### MyTooltipComponent.razor
```html
@ChildContent(TargetForwardRef)

<div class="my-tooltip">@Tooltip</div>

@functions {
    private ForwardRef TargetForwardRef {get;set;} = new ForwardRef();
    
    [Parameter]
    protected string Tooltip {get;set;}
    
    [Parameter]
    protected RenderFragment<ForwardRef> ChildContent {get;set;}   
    
    
    protected override async Task OnAfterRenderAsync()
    {
        // TargetForwardRef.Current will contain reference to target ElementRef 
        await js.InvokeAsync<object>("initTooltip", TargetForwardRef.Current);
    }
}
```

To obtain a `ElementRef` to the Html Element.
#### Index.razor
```html
<MyTooltipComponent Tooltip="My tooltip">
    <input ref="@context.Current" ... />
</MyTooltipComponent>
```

To obtain a `ElementRef` to the Custom Blazor Component your component should get `ForwardRef` as a parameter.

#### MyCustomComponent.razor
```html
<MyCustomComponent>
    <input ref="ForwardRef.Current" ... />
</MyTooltipComponent>

@functions {
    [Parameter]
    protected ForwardRef RefBack {get;set;}
    
    protected ElementRef Ref
    {
        set 
        {
            RefBack?.Set(value);
        }
    }
}
```
#### Index.razor
```html
<MyTooltipComponent Tooltip="My tooltip">
    <MyCustomComponent RefBack="@context" />
</MyTooltipComponent>
```

### Summary
In my opinion, this is one of the best `ElementRef` transfer techniques among the components.
That I used in the development of the [MatBlazor](https://www.matblazor.com) components for [Tooltip](https://www.matblazor.com/Tooltip) and [Menu](https://www.matblazor.com/Menu).
