# MyraTexturePacker
MyraTexturePacker is console utility for making a texture atlas.

## Installation

```bash
dotnet tool install --global myratexpack
```

## Update

```bash
dotnet tool update --global myratexpack
```
# Usage
`myratexpack <input_folder> <output_file> [width] [height]`

I.e.
`myratexpack "C:\Temp" "C:\Temp\my_atlas.png"`

That command will make MyraTexturePacker go over all images in the folder "C:\Temp" and create the texture atlas from it.

`width` and `height` are optional parameters. If they aren't provided, then default values 256x256 are used.
If the images don't fit on the atlas, then its size is doubled until the images fit.

The texture atlas will consist of two files: my_atlas.png(atlas image) and my_atlas.xmat(atlas definition in XML format).

MyraTexturePacker supports nine patch images. In order to use that feature, the input image name must have ".9" before the extension(i.e. `image.9.png`). Also such image must have 1px border with black lines marking strechable areas. 
See this link for thorough explanation of this feature: https://github.com/libgdx/libgdx/wiki/Ninepatches

Example set of input images(both ordinary and nine-patch): https://github.com/rds1983/Myra/tree/master/assets-raw

# Who Uses It?
https://github.com/rds1983/Myra
