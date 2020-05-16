﻿using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.ComponentModel;

using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Resources;

using SharpVectors.Runtime;
using SharpVectors.Renderers.Wpf;

namespace SharpVectors.Converters
{
    /// <summary>
    /// This is an <see langword="abstract"/> implementation of a markup extension that enables the creation
    /// of <see cref="DrawingImage"/> from SVG sources.
    /// </summary>
    [MarkupExtensionReturnType(typeof(DrawingImage))]
    public abstract class SvgImageBase : MarkupExtension
    {
        #region Protected Fields

        protected bool _textAsGeometry;
        protected bool _includeRuntime;
        protected bool _optimizePath;
        protected string _appName;
        protected CultureInfo _culture;

        #endregion

        #region Constructors and Destructor

        /// <summary>
        /// Initializes a new instance of the <see cref="SvgImageBase"/> 
        /// class with the default parameters.
        /// </summary>
        protected SvgImageBase()
        {
            _textAsGeometry = false;
            _includeRuntime = true;
            _optimizePath   = true;

            this.GetAppName();
        }

        #endregion

        #region Public Properties        

        /// <summary>
        /// Gets or sets a value indicating whether the path geometry is 
        /// optimized using the <see cref="StreamGeometry"/>.
        /// </summary>
        /// <value>
        /// This is <see langword="true"/> if the path geometry is optimized
        /// using the <see cref="StreamGeometry"/>; otherwise, it is 
        /// <see langword="false"/>. The default is <see langword="true"/>.
        /// </value>
        public bool OptimizePath
        {
            get {
                return _optimizePath;
            }
            set {
                _optimizePath = value;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the texts are rendered as
        /// path geometry.
        /// </summary>
        /// <value>
        /// This is <see langword="true"/> if texts are rendered as path 
        /// geometries; otherwise, this is <see langword="false"/>. The default
        /// is <see langword="false"/>.
        /// </value>
        public bool TextAsGeometry
        {
            get {
                return _textAsGeometry;
            }
            set {
                _textAsGeometry = value;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the <c>SharpVectors.Runtime.dll</c>
        /// classes are used in the generated output.
        /// </summary>
        /// <value>
        /// This is <see langword="true"/> if the <c>SharpVectors.Runtime.dll</c>
        /// classes and types are used in the generated output; otherwise, it is 
        /// <see langword="false"/>. The default is <see langword="true"/>.
        /// </value>
        /// <remarks>
        /// The use of the <c>SharpVectors.Runtime.dll</c> prevents the hard-coded
        /// font path generated by the <see cref="FormattedText"/> class, support
        /// for embedded images etc.
        /// </remarks>
        public bool IncludeRuntime
        {
            get {
                return _includeRuntime;
            }
            set {
                _includeRuntime = value;
            }
        }

        /// <summary>
        /// Gets or sets the main culture information used for rendering texts.
        /// </summary>
        /// <value>
        /// An instance of the <see cref="CultureInfo"/> specifying the main
        /// culture information for texts. The default is the English culture.
        /// </value>
        /// <remarks>
        /// <para>
        /// This is the culture information passed to the <see cref="FormattedText"/>
        /// class instance for the text rendering.
        /// </para>
        /// <para>
        /// The library does not currently provide any means of splitting texts
        /// into its multi-language parts.
        /// </para>
        /// </remarks>
        public CultureInfo CultureInfo
        {
            get {
                return _culture;
            }
            set {
                if (value != null)
                {
                    _culture = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the project or application name of the target assembly.
        /// </summary>
        /// <value>
        /// A string specifying the application project name.
        /// </value>
        /// <remarks>
        /// This is optional and is only used to resolve the resource Uri at the design time.
        /// </remarks>
        public string AppName
        {
            get {
                return _appName;
            }
            set {
                if (string.IsNullOrWhiteSpace(value))
                {
                    this.GetAppName();
                }
                else
                {
                    _appName = value;
                }
            }
        }

        #endregion

        #region Protected Methods

        /// <summary>
        /// This converts the SVG resource specified by the Uri to <see cref="DrawingGroup"/>.
        /// </summary>
        /// <param name="svgSource">A <see cref="Uri"/> specifying the source of the SVG resource.</param>
        /// <returns>A <see cref="DrawingGroup"/> of the converted SVG resource.</returns>
        protected virtual DrawingGroup GetDrawing(Uri svgSource)
        {
            string scheme = null;
            // A little hack to display preview in design mode: The design mode Uri is not absolute.
            bool designTime = DesignerProperties.GetIsInDesignMode(new DependencyObject());
            if (designTime && svgSource.IsAbsoluteUri == false)
            {
                scheme = "pack";
            }
            else
            {
                scheme = svgSource.Scheme;
            }
            if (string.IsNullOrWhiteSpace(scheme))
            {
                return null;
            }

            WpfDrawingSettings settings = new WpfDrawingSettings();
            settings.IncludeRuntime = _includeRuntime;
            settings.TextAsGeometry = _textAsGeometry;
            settings.OptimizePath   = _optimizePath;
            if (_culture != null)
            {
                settings.CultureInfo = _culture;
            }

            switch (scheme)
            {
                case "file":
                //case "ftp":
                case "https":
                case "http":
                    using (FileSvgReader reader = new FileSvgReader(settings))
                    {
                        DrawingGroup drawGroup = reader.Read(svgSource);

                        if (drawGroup != null)
                        {
                            return drawGroup;
                        }
                    }
                    break;
                case "pack":
                    StreamResourceInfo svgStreamInfo = null;
                    if (svgSource.ToString().IndexOf("siteoforigin", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        svgStreamInfo = Application.GetRemoteStream(svgSource);
                    }
                    else
                    {
                        svgStreamInfo = Application.GetResourceStream(svgSource);
                    }

                    Stream svgStream = (svgStreamInfo != null) ? svgStreamInfo.Stream : null;

                    if (svgStream != null)
                    {
                        string fileExt = Path.GetExtension(svgSource.ToString());
                        bool isCompressed = !string.IsNullOrWhiteSpace(fileExt) &&
                            string.Equals(fileExt, ".svgz", StringComparison.OrdinalIgnoreCase);

                        if (isCompressed)
                        {
                            using (svgStream)
                            {
                                using (var zipStream = new GZipStream(svgStream, CompressionMode.Decompress))
                                {
                                    using (FileSvgReader reader = new FileSvgReader(settings))
                                    {
                                        DrawingGroup drawGroup = reader.Read(zipStream);

                                        if (drawGroup != null)
                                        {
                                            return drawGroup;
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            using (svgStream)
                            {
                                using (FileSvgReader reader = new FileSvgReader(settings))
                                {
                                    DrawingGroup drawGroup = reader.Read(svgStream);

                                    if (drawGroup != null)
                                    {
                                        return drawGroup;
                                    }
                                }
                            }
                        }
                    }
                    break;
                case "data":
                    var sourceData = svgSource.OriginalString.Replace(" ", "");

                    int nColon = sourceData.IndexOf(":", StringComparison.OrdinalIgnoreCase);
                    int nSemiColon = sourceData.IndexOf(";", StringComparison.OrdinalIgnoreCase);
                    int nComma = sourceData.IndexOf(",", StringComparison.OrdinalIgnoreCase);

                    string sMimeType = sourceData.Substring(nColon + 1, nSemiColon - nColon - 1);
                    string sEncoding = sourceData.Substring(nSemiColon + 1, nComma - nSemiColon - 1);

                    if (string.Equals(sMimeType.Trim(), "image/svg+xml", StringComparison.OrdinalIgnoreCase)
                        && string.Equals(sEncoding.Trim(), "base64", StringComparison.OrdinalIgnoreCase))
                    {
                        string sContent = SvgObject.RemoveWhitespace(sourceData.Substring(nComma + 1));
                        byte[] imageBytes = Convert.FromBase64CharArray(sContent.ToCharArray(),
                            0, sContent.Length);
                        bool isGZiped = sContent.StartsWith(SvgObject.GZipSignature, StringComparison.Ordinal);
                        if (isGZiped)
                        {
                            using (var stream = new MemoryStream(imageBytes))
                            {
                                using (GZipStream zipStream = new GZipStream(stream, CompressionMode.Decompress))
                                {
                                    using (var reader = new FileSvgReader(settings))
                                    {
                                        DrawingGroup drawGroup = reader.Read(zipStream);
                                        if (drawGroup != null)
                                        {
                                            return drawGroup;
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            using (var stream = new MemoryStream(imageBytes))
                            {
                                using (var reader = new FileSvgReader(settings))
                                {
                                    DrawingGroup drawGroup = reader.Read(stream);
                                    if (drawGroup != null)
                                    {
                                        return drawGroup;
                                    }
                                }
                            }
                        }
                    }
                    break;
            }

            return null;
        }

        /// <summary>
        /// This converts the SVG resource specified by the Uri to <see cref="DrawingImage"/>.
        /// </summary>
        /// <param name="svgSource">A <see cref="Uri"/> specifying the source of the SVG resource.</param>
        /// <returns>A <see cref="DrawingImage"/> of the converted SVG resource.</returns>
        /// <remarks>
        /// This uses the <see cref="GetDrawing(Uri)"/> method to convert the SVG resource to <see cref="DrawingGroup"/>,
        /// which is then wrapped in <see cref="DrawingImage"/>.
        /// </remarks>
        protected virtual DrawingImage GetImage(Uri svgSource)
        {
            DrawingGroup drawGroup = this.GetDrawing(svgSource);
            if (drawGroup != null)
            {
                return new DrawingImage(drawGroup);
            }
            return null;
        }

        protected void GetAppName()
        {
            try
            {
                Assembly asm = this.GetEntryAssembly();

                if (asm != null)
                {
                    _appName = asm.GetName().Name;
                }
            }
            catch
            {
                // Issue #125
            }
        }

        protected Assembly GetEntryAssembly()
        {
            string XDesProc = "XDesProc"; // WPF designer process
            var comparer    = StringComparison.OrdinalIgnoreCase;

            Assembly asm = null;
            try
            {
                // Should work at runtime...
                asm = Assembly.GetEntryAssembly(); //...but mostly loading the design-time process: XDesProc.exe
                if (asm != null)
                {
                    var appName = asm.GetName().Name;
                    if (string.Equals(appName, XDesProc, comparer))
                    {
                        asm = null;
                    }
                }
                // Design time
                if (asm == null)
                {
                    asm = (
                          from assembly in AppDomain.CurrentDomain.GetAssemblies()
                          where !assembly.IsDynamic
                          let assmName = Path.GetFileName(assembly.CodeBase).Trim()
                          where assmName.EndsWith(".exe", comparer)
                          where !string.Equals(assmName, "XDesProc.exe", comparer) // should not be XDesProc.exe
                          select assembly
                          ).FirstOrDefault();

                    if (asm == null)
                    {
                        asm = Application.ResourceAssembly;
                        if (asm != null)
                        {
                            var appName = asm.GetName().Name;
                            if (string.Equals(appName, XDesProc, comparer))
                            {
                                asm = null;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (asm == null)
                {
                    asm = (
                          from assembly in AppDomain.CurrentDomain.GetAssemblies()
                          where !assembly.IsDynamic
                          let assmName = Path.GetFileName(assembly.CodeBase).Trim()
                          where assmName.EndsWith(".exe", comparer)
                          where !string.Equals(assmName, "XDesProc.exe", comparer) // should not be XDesProc.exe
                          select assembly
                          ).FirstOrDefault();
                }

                Trace.TraceError(ex.ToString());
            }
            return asm;
        }

        protected Assembly GetExecutingAssembly()
        {
            Assembly asm = null;
            try
            {
                asm = Assembly.GetExecutingAssembly();
            }
            catch
            {
                asm = Assembly.GetEntryAssembly();
            }
            return asm;
        }

        #endregion
    }
}
