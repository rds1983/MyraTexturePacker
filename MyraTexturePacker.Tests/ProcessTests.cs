using StbImageSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace MyraTexturePacker.Tests;

public class ProcessTests
{
	private readonly string _testOutputDir;
	private readonly string _inputDir;

	public ProcessTests()
	{
		_inputDir = Path.Combine(AppContext.BaseDirectory, "input");
		_testOutputDir = Path.Combine(AppContext.BaseDirectory, "output");

		CleanOutputDirectory();
	}

	private void CleanOutputDirectory()
	{
		if (!Directory.Exists(_testOutputDir))
		{
			Directory.CreateDirectory(_testOutputDir);
			return;
		}

		try
		{
			// Try to delete all files in the directory
			foreach (var file in Directory.GetFiles(_testOutputDir))
			{
				File.Delete(file);
			}
		}
		catch
		{
			// If cleanup fails, ignore - the test will use the directory as-is
		}
	}

	[Theory]
	[InlineData(128)]
	[InlineData(256)]
	[InlineData(512)]
	[InlineData(1024)]
	public void Process_GeneratesValidAtlas(int atlasSize)
	{
		// Arrange
		var outputFile = Path.Combine(_testOutputDir, $"atlas_{atlasSize}.png");
		var expectedXmlFile = Path.ChangeExtension(outputFile, "xmat");

		// Act
		Program.Process(_inputDir, outputFile, atlasSize, atlasSize);

		// Assert
		// Check output files exist
		Assert.True(File.Exists(outputFile), "Output PNG file was not created");
		Assert.True(File.Exists(expectedXmlFile), "Output XML file was not created");

		// Verify PNG file is valid and readable
		using var pngStream = File.OpenRead(outputFile);
		var atlasImage = ImageResult.FromStream(pngStream, ColorComponents.RedGreenBlueAlpha);
		Assert.NotNull(atlasImage);

		// Verify atlas size is either the requested size or a power-of-2 multiple if it was too small
		Assert.True(atlasImage.Width >= atlasSize, $"Atlas width {atlasImage.Width} is less than requested {atlasSize}");
		Assert.True(atlasImage.Height >= atlasSize, $"Atlas height {atlasImage.Height} is less than requested {atlasSize}");

		// Verify atlas dimensions are powers of 2 (doubling behavior)
		Assert.True(IsPowerOf2(atlasImage.Width), $"Atlas width {atlasImage.Width} is not a power of 2");
		Assert.True(IsPowerOf2(atlasImage.Height), $"Atlas height {atlasImage.Height} is not a power of 2");

		// Verify atlas width and height are equal
		Assert.Equal(atlasImage.Width, atlasImage.Height);

		// Verify pixel data exists
		Assert.True(atlasImage.Data.Length > 0, "Atlas image has no pixel data");

		// Verify XML structure
		var xmlDoc = XDocument.Load(expectedXmlFile);
		Assert.NotNull(xmlDoc.Root);
		Assert.Equal("TextureAtlas", xmlDoc.Root.Name.LocalName);

		// Verify XML points to the correct image
		var imageAttr = xmlDoc.Root.Attribute("Image");
		Assert.NotNull(imageAttr);
		Assert.Equal($"atlas_{atlasSize}.png", imageAttr.Value);

		// Verify all images from input are referenced in XML
		var inputFiles = Directory.GetFiles(_inputDir, "*.*", SearchOption.TopDirectoryOnly);
		var textureRegions = xmlDoc.Root.Elements("TextureRegion");
		var ninePatchRegions = xmlDoc.Root.Elements("NinePatchRegion");
		var totalRegions = textureRegions.Count() + ninePatchRegions.Count();

		Assert.True(totalRegions > 0, "No texture regions found in XML");
		Assert.True(totalRegions >= 10, $"Expected at least 10 regions, got {totalRegions}");
		Assert.Equal(inputFiles.Length, totalRegions);

		// Verify texture regions have required attributes and are named after input files
		var regionIds = new HashSet<string>();
		foreach (var region in textureRegions)
		{
			AssertRegionAttributes(region);
			regionIds.Add(region.Attribute("Id").Value);
		}

		// Verify nine-patch regions have required attributes and are named after input files
		foreach (var region in ninePatchRegions)
		{
			AssertRegionAttributes(region);
			AssertNinePatchAttributes(region);
			regionIds.Add(region.Attribute("Id").Value);
		}

		// Verify that regions correspond to input files
		foreach (var inputFile in inputFiles)
		{
			var fileName = Path.GetFileNameWithoutExtension(inputFile);
			// Remove .9 suffix for nine-patch files
			if (fileName.EndsWith(".9"))
			{
				fileName = fileName.Substring(0, fileName.Length - 2);
			}
			Assert.True(regionIds.Contains(fileName), $"Input file {inputFile} not found as region in atlas");
		}

		// Verify texture regions have valid coordinates
		foreach (var region in textureRegions.Concat(ninePatchRegions))
		{
			var left = int.Parse(region.Attribute("Left").Value);
			var top = int.Parse(region.Attribute("Top").Value);
			var width = int.Parse(region.Attribute("Width").Value);
			var height = int.Parse(region.Attribute("Height").Value);

			// Verify coordinates are within atlas bounds
			Assert.True(left >= 0, $"Region {region.Attribute("Id").Value} has negative Left coordinate");
			Assert.True(top >= 0, $"Region {region.Attribute("Id").Value} has negative Top coordinate");
			Assert.True(width > 0, $"Region {region.Attribute("Id").Value} has non-positive width");
			Assert.True(height > 0, $"Region {region.Attribute("Id").Value} has non-positive height");
			Assert.True(left + width <= atlasImage.Width,
				$"Region {region.Attribute("Id").Value} extends beyond atlas width");
			Assert.True(top + height <= atlasImage.Height,
				$"Region {region.Attribute("Id").Value} extends beyond atlas height");
		}
	}

	private bool IsPowerOf2(int value)
	{
		return value > 0 && (value & (value - 1)) == 0;
	}

	private void AssertRegionAttributes(XElement region)
	{
		var id = region.Attribute("Id");
		var left = region.Attribute("Left");
		var top = region.Attribute("Top");
		var width = region.Attribute("Width");
		var height = region.Attribute("Height");

		Assert.NotNull(id);
		Assert.NotNull(left);
		Assert.NotNull(top);
		Assert.NotNull(width);
		Assert.NotNull(height);

		Assert.False(string.IsNullOrEmpty(id.Value));
		Assert.True(int.TryParse(left.Value, out _));
		Assert.True(int.TryParse(top.Value, out _));
		Assert.True(int.TryParse(width.Value, out _));
		Assert.True(int.TryParse(height.Value, out _));
	}

	private void AssertNinePatchAttributes(XElement region)
	{
		var left = region.Attribute("NinePatchLeft");
		var top = region.Attribute("NinePatchTop");
		var right = region.Attribute("NinePatchRight");
		var bottom = region.Attribute("NinePatchBottom");

		Assert.NotNull(left);
		Assert.NotNull(top);
		Assert.NotNull(right);
		Assert.NotNull(bottom);

		Assert.True(int.TryParse(left.Value, out _));
		Assert.True(int.TryParse(top.Value, out _));
		Assert.True(int.TryParse(right.Value, out _));
		Assert.True(int.TryParse(bottom.Value, out _));
	}
}
