using StbImageSharp;
using StbImageWriteSharp;
using StbRectPackSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace MyraTexturePacker
{
	static class Program
	{
		private const string IdName = "Id";
		private const string TextureAtlasName = "TextureAtlas";
		private const string ImageName = "Image";
		private const string TextureRegionName = "TextureRegion";
		private const string NinePatchRegionName = "NinePatchRegion";
		private const string LeftName = "Left";
		private const string TopName = "Top";
		private const string WidthName = "Width";
		private const string HeightName = "Height";
		private const string NinePatchLeftName = "NinePatchLeft";
		private const string NinePatchTopName = "NinePatchTop";
		private const string NinePatchRightName = "NinePatchRight";
		private const string NinePatchBottomName = "NinePatchBottom";

		class ImageInfo
		{
			public string Path;
			public ImageResult Image;
			public bool IsNinePatch;
			public int NinePatchLeft, NinePatchRight, NinePatchTop, NinePatchBottom;
		}

		private enum OutputType
		{
			Jpg,
			Png,
			Tga,
			Bmp,
			Hdr
		}

		private static void Process(string inputFolder, string outputFile)
		{
			var ext = Path.GetExtension(outputFile);
			if (string.IsNullOrEmpty(ext))
			{
				throw new Exception("Output file lacks extension. Hence it is not possible to determine output file type");
			}

			if (ext.StartsWith("."))
			{
				ext = ext.Substring(1);
			}

			ext = ext.ToLower();
			OutputType outputType = OutputType.Png;
			switch (ext)
			{
				case "jpg":
				case "jpeg":
					outputType = OutputType.Jpg;
					break;
				case "png":
					outputType = OutputType.Png;
					break;
				case "tga":
					outputType = OutputType.Tga;
					break;
				case "bmp":
					outputType = OutputType.Bmp;
					break;
				case "hdr":
					outputType = OutputType.Hdr;
					break;
				default:
					throw new Exception("Output format '" + ext + "' is not supported.");
			}

			var allFiles = Directory.EnumerateFiles(inputFolder, "*.*", SearchOption.TopDirectoryOnly).ToArray();

			var imageFiles = new List<string>();

			foreach (var file in allFiles)
			{
				ext = Path.GetExtension(file);
				if (string.IsNullOrEmpty(ext))
				{
					continue;
				}

				if (ext.StartsWith("."))
				{
					ext = ext.Substring(1);
				}

				ext = ext.ToLower();

				if (ext != "bmp" && ext != "jpg" && ext != "png" &&
					ext != "jpg" && ext != "psd" && ext != "pic" &&
					ext != "tga" && ext != "hdr")
				{
					// Not an image
					continue;
				}

				imageFiles.Add(file);
			}

			if (imageFiles.Count == 0)
			{
				throw new Exception("No image files found at " + inputFolder);
			}

			Console.WriteLine("{0} image files found at {1}.", imageFiles.Count, inputFolder);

			var width = 256;
			var height = 256;

			Console.WriteLine("Atlas Size: {0}x{1}", width, height);

			var packer = new Packer(width, height);
			foreach (var file in imageFiles)
			{
				Console.WriteLine("Processing {0}...", file);

				ImageResult image;
				using (var stream = File.OpenRead(file))
				{
					image = ImageResult.FromStream(stream, StbImageSharp.ColorComponents.RedGreenBlueAlpha);
				}

				var imageInfo = new ImageInfo
				{
					Path = file,
					Image = image
				};

				var name = Path.GetFileNameWithoutExtension(file);
				if (name.EndsWith(".9"))
				{
					Console.WriteLine("Nine Patch");

					// Nine Patch
					if (image.Width < 2 || image.Height < 2)
					{
						throw new Exception(string.Format("Nine Patch image lacks 1px border with black stretch lines"));
					}

					imageInfo.IsNinePatch = true;

					// Determine Left and Right
					var foundBlack = false;
					int blackSize = 0;
					for (var i = 0; i < image.Width; ++i)
					{
						var pos = i * 4;
						if (image.Data[pos] == 0 && image.Data[pos + 3] > 128)
						{
							if (!foundBlack)
							{
								imageInfo.NinePatchLeft = i - 1;
								foundBlack = true;
							}
							++blackSize;
						}
						else if (foundBlack)
						{
							break;
						}
					}

					imageInfo.NinePatchRight = image.Width - 2 - imageInfo.NinePatchLeft - blackSize;

					// Determine Top and Bottom
					foundBlack = false;
					blackSize = 0;
					for (var i = 0; i < image.Height; ++i)
					{
						var pos = i * image.Width * 4;
						if (image.Data[pos] == 0 && image.Data[pos + 3] > 128)
						{
							if (!foundBlack)
							{
								imageInfo.NinePatchTop = i - 1;
								foundBlack = true;
							}
							++blackSize;
						}
						else if (foundBlack)
						{
							break;
						}
					}

					imageInfo.NinePatchBottom = image.Height - 2 - imageInfo.NinePatchTop - blackSize;

					Console.WriteLine("Left: {0}, Right: {1}, Top: {2}, Bottom: {3}", imageInfo.NinePatchLeft, imageInfo.NinePatchRight, imageInfo.NinePatchTop, imageInfo.NinePatchBottom);

					if (imageInfo.NinePatchLeft == 0 && imageInfo.NinePatchRight == 0 &&
						imageInfo.NinePatchTop == 0 && imageInfo.NinePatchBottom == 0)
					{
						// Not a nine patch
						imageInfo.IsNinePatch = false;
					}

					// Erase info border
					var newWidth = image.Width - 2;
					var newHeight = image.Height - 2;
					var newData = new byte[newWidth * newHeight * 4];
					for (var y = 0; y < newHeight; ++y)
					{
						for (var x = 0; x < newWidth; ++x)
						{
							var sourcePos = ((y + 1) * image.Width + (x + 1)) * 4;
							var destPos = ((y * newWidth) + x) * 4;

							for (var c = 0; c < 4; ++c)
							{
								newData[destPos + c] = image.Data[sourcePos + c];
							}
						}
					}

					image.Width = newWidth;
					image.Height = newHeight;
					image.Data = newData;
				}

				Console.WriteLine("Size: {0}x{1}, Components: {2}", image.Width, image.Height, image.SourceComp);

				var packRectangle = packer.PackRect(image.Width, image.Height, imageInfo);

				// Double the size of the packer until the new rectangle will fit
				while (packRectangle == null)
				{
					Packer newPacker = new Packer(packer.Width * 2, packer.Height * 2);

					Console.WriteLine("Image didnt fit. Thus atlas size had been doubled: {0}x{1}", newPacker.Width, newPacker.Height);

					// Place existing rectangles
					foreach (PackerRectangle existingRect in packer.PackRectangles)
					{
						newPacker.PackRect(existingRect.Width, existingRect.Height, existingRect.Data);
					}

					// Now dispose old packer and assign new one
					packer.Dispose();
					packer = newPacker;

					// Try to fit the rectangle again
					packRectangle = packer.PackRect(image.Width, image.Height, imageInfo);
				}
			}

			// All images had been packed
			// Now build up the atlas bitmap
			var bitmap = new byte[packer.Width * packer.Height * 4];
			foreach (var packRectangle in packer.PackRectangles)
			{
				var imageInfo = (ImageInfo)packRectangle.Data;

				var image = imageInfo.Image;

				// Draw image on its position
				for (var y = 0; y < image.Height; ++y)
				{
					for (var x = 0; x < image.Width; ++x)
					{
						var sourcePos = (y * image.Width + x) * 4;
						var destPos = (((y + packRectangle.Y) * packer.Width) + x + packRectangle.X) * 4;

						for (var c = 0; c < 4; ++c)
						{
							bitmap[destPos + c] = image.Data[sourcePos + c];
						}
					}
				}
			}

			using (var stream = File.Create(outputFile))
			{
				var imageWriter = new ImageWriter();
				switch (outputType)
				{
					case OutputType.Jpg:
						imageWriter.WriteJpg(bitmap, packer.Width, packer.Height, StbImageWriteSharp.ColorComponents.RedGreenBlueAlpha, stream, 90);
						break;
					case OutputType.Png:
						imageWriter.WritePng(bitmap, packer.Width, packer.Height, StbImageWriteSharp.ColorComponents.RedGreenBlueAlpha, stream);
						break;
					case OutputType.Tga:
						imageWriter.WriteTga(bitmap, packer.Width, packer.Height, StbImageWriteSharp.ColorComponents.RedGreenBlueAlpha, stream);
						break;
					case OutputType.Bmp:
						imageWriter.WriteBmp(bitmap, packer.Width, packer.Height, StbImageWriteSharp.ColorComponents.RedGreenBlueAlpha, stream);
						break;
					case OutputType.Hdr:
						imageWriter.WriteHdr(bitmap, packer.Width, packer.Height, StbImageWriteSharp.ColorComponents.RedGreenBlueAlpha, stream);
						break;
				}
			}



			// Generate XML
			var doc = new XDocument();
			var root = new XElement(TextureAtlasName);
			root.SetAttributeValue(ImageName, Path.GetFileName(outputFile));
			doc.Add(root);

			foreach (var packRectangle in packer.PackRectangles)
			{
				var imageInfo = (ImageInfo)packRectangle.Data;

				var entry = new XElement(imageInfo.IsNinePatch ? NinePatchRegionName : TextureRegionName);

				var id = Path.GetFileNameWithoutExtension(imageInfo.Path);
				if (id.EndsWith(".9"))
				{
					id = id.Substring(0, id.Length - 2);
				}
				entry.SetAttributeValue(IdName, id);
				entry.SetAttributeValue(LeftName, packRectangle.X);
				entry.SetAttributeValue(TopName, packRectangle.Y);
				entry.SetAttributeValue(WidthName, packRectangle.Width);
				entry.SetAttributeValue(HeightName, packRectangle.Height);

				if (imageInfo.IsNinePatch)
				{
					entry.SetAttributeValue(NinePatchLeftName, imageInfo.NinePatchLeft);
					entry.SetAttributeValue(NinePatchTopName, imageInfo.NinePatchTop);
					entry.SetAttributeValue(NinePatchRightName, imageInfo.NinePatchRight);
					entry.SetAttributeValue(NinePatchBottomName, imageInfo.NinePatchBottom);
				}

				root.Add(entry);
			}

			var outputFileXml = Path.ChangeExtension(outputFile, "xmat");

			doc.Save(outputFileXml);
		}

		public static void Main(string[] args)
		{
			if (args.Length < 2)
			{
				Console.WriteLine("Usage: MyraTexturePacker.exe <input_folder> <output_file>");
				return;
			}

			try
			{
				Process(args[0], args[1]);
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex);
			}
		}
	}
}
