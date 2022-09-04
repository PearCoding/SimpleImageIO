﻿using System.IO;
using System.Reflection;
using System.Text;

namespace SimpleImageIO.FlipBook;

/// <summary>
/// Generates an HTML image viewer to compare equal-size images by flipping through them. Can be used through
/// static functions that generate the flip book from a single array, or through a fluent API.
/// </summary>
public class FlipBook
{
    static string ReadResourceText(string filename)
    {
        var assembly = typeof(FlipBook).GetTypeInfo().Assembly;
        var stream = assembly.GetManifestResourceStream("SimpleImageIO." + filename);
        if (stream == null)
            throw new FileNotFoundException("resource file not found", filename);
        return new StreamReader(stream).ReadToEnd();
    }

    static string MakeComparisonHtml(params (string Name, string EncodedData)[] images)
    {
        StringBuilder html = new();
        html.AppendLine("<div>");

        // For smoother Jupyter / VSCode experience, we add the style to every single viewer
        html.AppendLine("<style>" + ReadResourceText("style.css") + "</style>");

        html.AppendLine("  <div class='method-list'>");
        for (int i = 0; i < images.Length; ++i)
        {
            string visible = "";
            if (i == 0) visible = "visible";
            html.AppendLine($"    <button class='method-label method-{i+1} {visible}'><kbd>{i+1}</kbd> {images[i].Name}</button>");
        }
        html.AppendLine("  </div>");

        html.AppendLine("  <div tabindex='1' class='image-container'>");
        html.AppendLine("    <div class='image-placer'>");
        for (int i = 0; i < images.Length; ++i)
        {
            string visible = "";
            if (i == 0) visible = "visible";
            html.AppendLine($"      <img draggable='false' class='image image-{i+1} {visible}' src='{images[i].EncodedData}' />");
        }
        html.AppendLine("    </div>");
        html.AppendLine("  </div>");
        html.AppendLine("</div>");
        html.AppendLine($"<script> initImageViewers({images.Length}); </script>");
        return html.ToString();
    }

    /// <summary>
    /// Tone mapping or false coloring function signature. Should create and return a new image, the pixels of
    /// which are given by some operation performed on the input image.
    /// </summary>
    /// <param name="input">The original image</param>
    /// <returns>The tone mapped or otherwise modified result</returns>
    public delegate ImageBase ToneMapper(ImageBase input);

    static string MakeHelper<T>(ToneMapper toneMapper, IEnumerable<(string Name, T Image)> images)
    where T : ImageBase
    {
        var data = new List<(string Name, string EncodedData)>();
        foreach (var img in images)
        {
            var toneMapped = toneMapper?.Invoke(img.Image) ?? (img.Image as ImageBase);
            data.Add((img.Name, "data:image/png;base64," + toneMapped.AsBase64Png()));
        }
        return MakeComparisonHtml(data.ToArray());
    }

    /// <summary>
    /// Creates a flip book image viewer from an array of named images
    /// </summary>
    /// <param name="images">Tuples of name and image data</param>
    /// <returns>HTML code for the flip book</returns>
    public static string Make<T>(params (string Name, T Image)[] images) where T : ImageBase
    => MakeHelper(null, images);

    /// <summary>
    /// Creates a flip book image viewer from an array of named images. Applies a tone mapping operation on
    /// every image.
    /// </summary>
    /// <param name="toneMapper">The operation to run on every image</param>
    /// <param name="images">Tuples of name and image data</param>
    /// <returns>HTML code for the flip book</returns>
    public static string Make<T>(ToneMapper toneMapper, params (string Name, T Image)[] images) where T : ImageBase
    => MakeHelper(toneMapper, images);

    /// <summary>
    /// Creates a flip book image viewer from an array of named images
    /// </summary>
    /// <param name="images">Tuples of name and image data</param>
    /// <returns>HTML code for the flip book</returns>
    public static string Make<T>(IEnumerable<(string Name, T Image)> images) where T : ImageBase
    => MakeHelper(null, images);

    /// <summary>
    /// Creates a flip book image viewer from an array of named images. Applies a tone mapping operation on
    /// every image.
    /// </summary>
    /// <param name="toneMapper">The operation to run on every image</param>
    /// <param name="images">Tuples of name and image data</param>
    /// <returns>HTML code for the flip book</returns>
    public static string Make<T>(ToneMapper toneMapper, IEnumerable<(string Name, T Image)> images) where T : ImageBase
    => MakeHelper(toneMapper, images);

    /// <summary>
    /// Creates a flip book image viewer from an array of named images
    /// </summary>
    /// <param name="images">Tuples of name and image data</param>
    /// <returns>HTML code for the flip book</returns>
    public static string Make<T>(IEnumerable<Tuple<string, T>> images) where T : ImageBase
    => Make(null, images);

    /// <summary>
    /// Creates a flip book image viewer from an array of named images. Applies a tone mapping operation on
    /// every image.
    /// </summary>
    /// <param name="toneMapper">The operation to run on every image</param>
    /// <param name="images">Tuples of name and image data</param>
    /// <returns>HTML code for the flip book</returns>
    public static string Make<T>(ToneMapper toneMapper, IEnumerable<Tuple<string, T>> images) where T : ImageBase
    {
        var imageObjects = new List<(string, ImageBase)>();
        foreach (var (name, img) in images)
        {
            imageObjects.Add((name, img as ImageBase));
        }
        return MakeHelper(toneMapper, imageObjects);
    }

    /// <summary>
    /// Creates a flip book image viewer from an array of named images. Images are loaded from file based on
    /// the given file names.
    /// </summary>
    /// <param name="images">Tuples of name and image filename</param>
    /// <returns>HTML code for the flip book</returns>
    public static string Make(params (string Name, string Filename)[] images)
    => Make(null, images);

    /// <summary>
    /// Creates a flip book image viewer from an array of named images. Images are loaded from file based on
    /// the given file names. Applies a tone mapping operation on every image.
    /// </summary>
    /// <param name="toneMapper">The operation to run on every image</param>
    /// <param name="images">Tuples of name and image filename</param>
    /// <returns>HTML code for the flip book</returns>
    public static string Make(ToneMapper toneMapper, params (string Name, string Filename)[] images)
    {
        var imageObjects = new List<(string, ImageBase)>();
        foreach (var img in images)
        {
            imageObjects.Add((img.Name, new RgbImage(img.Filename)));
        }
        return MakeHelper(toneMapper, imageObjects);
    }

    /// <summary>
    /// Creates the HTML, JS, and CSS code controlling the flip viewer logic. Needs to be added once in
    /// the resulting .html file.
    ///
    /// Ideally, this should be added exactly once inside the &lt;head&gt;, but most (or all?) browsers accept it
    /// if this is added multiple times or in arbitrary locations (i.e., inside the &lt;body&gt; works fine, too).
    /// </summary>
    /// <returns>HTML code as a string</returns>
    public static string MakeHeader()
    {
        string html = "<script>" + ReadResourceText("imageViewer.js") + "</script>";
        html += "<style>" + ReadResourceText("style.css") + "</style>";
        return html;
    }

    List<(string Name, ImageBase Image)> images = new();

    /// <summary>
    /// Syntactic sugar to create a new object of this class. Makes the fluent API more readable.
    /// </summary>
    public static FlipBook New => new FlipBook();

    /// <summary>
    /// Adds a new image to this flip book.
    /// </summary>
    /// <param name="name">Name of the new image</param>
    /// <param name="image">Image object, must have the same resolution as existing images</param>
    /// <returns>This object (fluent API)</returns>
    public FlipBook Add(string name, ImageBase image)
    {
        if (images.Count > 0 && (images[0].Image.Width != image.Width || images[0].Image.Height != image.Height))
            throw new ArgumentException("Image resolution does not match", nameof(image));
        images.Add((name, image));
        return this;
    }

    /// <summary>
    /// Generates the HTML code for the flip book with the current set of images
    /// </summary>
    /// <returns>HTML code</returns>
    public override string ToString() => Make(images.ToArray());
}
