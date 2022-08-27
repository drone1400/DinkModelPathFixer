# DinkModelPathFixer
- A quick and dirty tool for parsing old 3D Studio Max files and replacing absolute paths with relative paths...
- This is intended to be used with the DinkSmallwood model files from the 90s!

## How to use?
- Simply copy it and run it in the "DinkSmallwood3DSMaxModelsAndEverything" folder
- ...or pass the folder path as the first argument using CMD: ex: ```DinkModelPathFixer "D:\MyStuff\Idk\DinkSmallwood3DSMaxModelsAndEverything\"```

## How it works?
- It first finds all the .max files in subfolders of the given path
- It then finds all texture files (tif, psd, tga, eps, bmp) in the subfolders
- It creates a dictionary of the texture file names
- For each model file, it reads it byte by byte looking for an absolute path encoded as Unicode, for example: ```43 00 3A 00 5C 00```
- After finding the start of an absolute path, it looks for the end of the path, marked by the byte sequence ``40 12``
- It then parses the found file path, and tries to match it to a texture file from the dictionary
- If a matching texture file is found, the absolute path is replaced with a path relative to the .max model file itself
- In the case of having multiple texture files with the same file name, the one with the most similar path is used

## Possible errors
There are currently 3 possible parsing errors:
- ERR_TEXTURE_NOT_FOUND - This means a texture file was not found for the given path. Note, since I am not analyzing if the path corresponds to an actual texture or some other rendering metadata or whatever, this might not be so important...
- ERR_RELATIVE_PATH - This means the relative path could not fit in the replaced absolute path length, it means you need to manually edit the paths in an old copy of 3DS Max or something
- ERR_OFFSET - This means the start of an absolute path was found, but not the expected end byte sequence... it means you need to manually edit the paths in an old copy of 3DS Max

After running the program, search for these error codes through the log file to find which files were affected