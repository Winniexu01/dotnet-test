﻿// <auto-generated/>
#pragma warning disable 1591
namespace Test
{
    #line default
    using global::System;
    using global::System.Collections.Generic;
    using global::System.Linq;
    using global::System.Threading.Tasks;
    using global::Microsoft.AspNetCore.Components;
#nullable restore
#line (1,2)-(1,15) "x:\dir\subdir\Test\TestComponent.cshtml"
using Models;

#nullable disable
    #line default
    #line hidden
    #nullable restore
    public partial class TestComponent : global::Microsoft.AspNetCore.Components.ComponentBase
    #nullable disable
    {
        #pragma warning disable 1998
        protected override void BuildRenderTree(global::Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder __builder)
        {
            __builder.OpenComponent<global::Test.Grid<
#nullable restore
#line (2,14)-(2,29) "x:\dir\subdir\Test\TestComponent.cshtml"
WeatherForecast

#line default
#line hidden
#nullable disable
            >>(0);
            __builder.AddComponentParameter(1, "Items", 
#nullable restore
#line (2,40)-(2,70) "x:\dir\subdir\Test\TestComponent.cshtml"
Array.Empty<WeatherForecast>()

#line default
#line hidden
#nullable disable
            );
            __builder.AddAttribute(2, "ColumnsTemplate", (global::Microsoft.AspNetCore.Components.RenderFragment)((__builder2) => {
                global::__Blazor.Test.TestComponent.TypeInference.CreateColumn_0(__builder2, 3, default(WeatherForecast)!, 4, "Date", 5, "Date", 6, "d", 7, "10rem");
            }
            ));
            __builder.CloseComponent();
        }
        #pragma warning restore 1998
    }
}
namespace __Blazor.Test.TestComponent
{
    #line hidden
    internal static class TypeInference
    {
        public static void CreateColumn_0<TItem>(global::Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder __builder, int seq, TItem __syntheticArg0, int __seq0, global::System.Object __arg0, int __seq1, global::System.String __arg1, int __seq2, global::System.Object __arg2, int __seq3, global::System.Object __arg3)
            where TItem : global::System.Collections.Generic.IEnumerable<TItem>
        {
        __builder.OpenComponent<global::Test.Column<TItem>>(seq);
        __builder.AddComponentParameter(__seq0, "Title", __arg0);
        __builder.AddComponentParameter(__seq1, nameof(global::Test.Column<TItem>.
#nullable restore
#line (4,30)-(4,39) "x:\dir\subdir\Test\TestComponent.cshtml"
FieldName

#line default
#line hidden
#nullable disable
        ), __arg1);
        __builder.AddComponentParameter(__seq2, "Format", __arg2);
        __builder.AddComponentParameter(__seq3, "Width", __arg3);
        __builder.CloseComponent();
        }
    }
}
#pragma warning restore 1591
