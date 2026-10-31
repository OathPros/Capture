using System;
using System.Drawing;
using System.Diagnostics;
using Capture.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
namespace Capture.App;
internal sealed record TargetSnapshot(int X,int Y,long Timestamp,string Type,string? Label,ControlBounds? Bounds,bool Password);
internal static class TargetResolver
{
    internal static TargetSnapshot? Resolve(UIA3Automation automation,int x,int y)
    {
        try
        {
            var leaf=automation.FromPoint(new Point(x,y));
            if(leaf.Properties.IsPassword.ValueOrDefault)return new(x,y,Stopwatch.GetTimestamp(),"Edit",null,null,true);
            AutomationElement? target=leaf;
            for(int depth=0;target is not null && depth<5;depth++,target=target.Parent)
            {
                var type=target.ControlType.ToString();
                if(type is not ("Button" or "MenuItem" or "TabItem" or "Hyperlink" or "CheckBox" or "RadioButton" or "ComboBox" or "Edit" or "ListItem" or "TreeItem"))continue;
                var box=target.BoundingRectangle;
                if(box.Width<3||box.Height<3||x<box.X-2||x>box.Right+2||y<box.Y-2||y>box.Bottom+2)continue;
                if(target.Properties.IsPassword.ValueOrDefault)return new(x,y,Stopwatch.GetTimestamp(),type,null,null,true);
                string? label;
                // Never request Edit.Name or ValuePattern: use its separately exposed static label only.
                if(type=="Edit")
                {
                    var labelElement=target.Properties.LabeledBy.ValueOrDefault;
                    label=labelElement is not null && labelElement.ControlType.ToString()=="Text"?labelElement.Name:null;
                    label??=StepText.FieldLabelFromId(target.Properties.AutomationId.ValueOrDefault);
                }
                else label=target.Name;
                return new(x,y,Stopwatch.GetTimestamp(),type,StepText.CleanLabel(label),new(box.X,box.Y,box.Width,box.Height),false);
            }
        }
        catch { /* Unresponsive/stale providers fall back to a review-marked click ring. */ }
        return null;
    }
    internal static bool Matches(TargetSnapshot? target,int x,int y,long timestamp)=>target is not null && target.Timestamp<=timestamp && Stopwatch.GetElapsedTime(target.Timestamp,timestamp).TotalMilliseconds<600 && Math.Abs(target.X-x)<32 && Math.Abs(target.Y-y)<32 && (target.Password || target.Bounds is not null && x>=target.Bounds.X && x<=target.Bounds.X+target.Bounds.Width && y>=target.Bounds.Y && y<=target.Bounds.Y+target.Bounds.Height);
}
