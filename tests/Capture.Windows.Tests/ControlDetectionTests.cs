using System.Drawing;
using Capture.App;
using Capture.Core;
using FlaUI.UIA3;
using Xunit;
using Forms=System.Windows.Forms;
namespace Capture.Windows.Tests;
public sealed class ControlDetectionTests
{
    [Fact] public async Task ActualWindowsControlsSupplyLabelsAndBoundsWithoutReadingEditableNamesOrValues()
    {
        var ready=new TaskCompletionSource<(Forms.Form Form,Rectangle Button,Rectangle Field)>(TaskCreationOptions.RunContinuationsAsynchronously);
        var ui=new Thread(()=>
        {
            try
            {
                using var form=new Forms.Form{Text="Synthetic Capture UIA fixture",StartPosition=Forms.FormStartPosition.Manual,Location=new Point(60,60),Size=new Size(460,300),TopMost=true};
                var button=new Forms.Button{Text="Save draft",Name="saveDraftButton",AccessibleName="Save draft",Location=new Point(30,30),Size=new Size(220,50)};
                var label=new Forms.Label{Text="Article title",Location=new Point(30,105),Size=new Size(200,25),TabIndex=1};
                var field=new Forms.TextBox{Name="articleTitleTextBox",AccessibleName="DO NOT READ EDIT NAME",Text="SYNTHETIC TYPED VALUE",Location=new Point(30,140),Size=new Size(270,40),TabIndex=2};
                form.Controls.Add(button);form.Controls.Add(label);form.Controls.Add(field);
                form.Shown+=(_,_)=>ready.SetResult((form,button.RectangleToScreen(button.ClientRectangle),field.RectangleToScreen(field.ClientRectangle)));
                Forms.Application.Run(form);
            }
            catch(Exception ex){ready.TrySetException(ex);}
        }){IsBackground=true};ui.SetApartmentState(ApartmentState.STA);ui.Start();
        var fixture=await ready.Task.WaitAsync(TimeSpan.FromSeconds(15));
        try
        {
            await Task.Delay(200);
            using var automation=new UIA3Automation();
            var b=fixture.Button;var target=TargetResolver.Resolve(automation,b.X+b.Width/2,b.Y+b.Height/2);Assert.NotNull(target);Assert.Equal("Button",target.Type);Assert.Equal("Save draft",target.Label);Assert.InRange(target.Bounds!.Width,180,240);Assert.InRange(target.Bounds.Height,40,65);
            var highlight=CaptureTargets.Highlight(b.X+b.Width/2,b.Y+b.Height/2,target.Bounds,new(0,0,1920,1080));Assert.Equal("highlight",highlight.Kind);Assert.True(highlight.Width>180/1920.0);
            var f=fixture.Field;var input=TargetResolver.Resolve(automation,f.X+f.Width/2,f.Y+f.Height/2);Assert.NotNull(input);Assert.Equal("Edit",input.Type);Assert.Equal("Article title",input.Label);Assert.DoesNotContain("TYPED",input.Label);Assert.DoesNotContain("EDIT NAME",input.Label);
        }
        finally{fixture.Form.BeginInvoke(()=>fixture.Form.Close());ui.Join(TimeSpan.FromSeconds(5));}
    }
}
