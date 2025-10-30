using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace SimpleGitVersion.MSBuild.Tests;

/// <summary>
/// System.Xml related helpers.
/// </summary>
public static class XmlHelper
{
    static readonly UTF8Encoding _uTF8EncodingNoBOM = new UTF8Encoding( false );

    /// <summary>
    /// Saves the document without the <c>&lt;?xml version="1.0" encoding="utf-8"?&gt;</c> declaration
    /// and ensures that the save file doesn't start with the Uf8 BOM. 
    /// <para>
    /// To avoid "white-space  only" commits, <see cref="XDocument.Load(string, LoadOptions)"/> with <see cref="LoadOptions.PreserveWhitespace"/>
    /// must be used.
    /// </para>
    /// </summary>
    /// <param name="doc">This documentation.</param>
    /// <param name="path">The file path to save.</param>
    /// <param name="saveOptions">Save options.</param>
    public static void SaveWithoutXmlDeclaration( this XDocument doc, string path, SaveOptions saveOptions = SaveOptions.None )
    {
        XmlWriterSettings xws = new XmlWriterSettings { OmitXmlDeclaration = true };
        if( (saveOptions & SaveOptions.DisableFormatting) == 0 ) xws.Indent = true;
        if( (saveOptions & SaveOptions.OmitDuplicateNamespaces) != 0 ) xws.NamespaceHandling |= NamespaceHandling.OmitDuplicates;
        xws.Encoding = _uTF8EncodingNoBOM;
        using( XmlWriter xw = XmlWriter.Create( path, xws ) )
            doc.Save( xw );
    }
}
