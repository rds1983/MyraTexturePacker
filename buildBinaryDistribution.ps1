$version = select-string -Path 'src/MyraTexturePacker.csproj' -Pattern '<Version>(.*)<\/Version>' -AllMatches | % { $_.Matches } | % { $_.Groups[1].Value }
echo "Version: $version"

# Recreate "ZipPackage"
Remove-Item -Recurse -Force "ZipPackage" -ErrorAction Ignore
Remove-Item -Recurse -Force "Myra.$version" -ErrorAction Ignore

New-Item -ItemType directory -Path "ZipPackage"

# Copy-Item -Path files
Copy-Item -Path "src\bin\Release\net45\MyraTexturePacker.exe" -Destination "ZipPackage"
Copy-Item -Path "src\bin\Release\net45\StbImageSharp.dll" -Destination "ZipPackage"
Copy-Item -Path "src\bin\Release\net45\StbImageWriteSharp.dll" -Destination "ZipPackage"
Copy-Item -Path "src\bin\Release\net45\StbRectPackSharp.dll" -Destination "ZipPackage"


# Compress
Rename-Item "ZipPackage" "MyraTexturePacker.$version"
Compress-Archive -Path "MyraTexturePacker.$version" -DestinationPath "MyraTexturePacker.$version.zip" -Force

# Delete the folder
Remove-Item -Recurse -Force "MyraTexturePacker.$version"