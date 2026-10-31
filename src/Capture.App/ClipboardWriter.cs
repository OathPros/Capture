using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Capture.Core;
namespace Capture.App;
public static class ClipboardWriter
{
    public static void HtmlCode(string code)
    {
        if(code.Length>128*1024*1024)throw new InvalidOperationException("This guide is too large for the clipboard. Use HTML export instead.");
        var data=new DataObject();data.SetData(DataFormats.UnicodeText,code);Write(data);
    }
    public static void WordSteps(WordClipboardContent content)
    {
        var data=new DataObject();data.SetData(DataFormats.Rtf,content.Rtf);data.SetData(DataFormats.UnicodeText,content.Text);Write(data);
    }
    private static void Write(DataObject data)
    {
        try {Clipboard.SetDataObject(data,true,10,100);}
        catch(ExternalException){throw new InvalidOperationException("The Windows clipboard is busy. Try copying again.");}
    }
}
