// SPDX-License-Identifier: MPL-2.0
#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Slices the approved transparent artwork into individual PNG assets, without repainting it.
public static class SymbolicSpriteImport
{
    const string Output="Assets/Art/Pieces/Symbolic";
    const float PieceDiameter=4.37f; // existing prefab hit circle: radius 2.185, local scale .05
    [MenuItem("Tools/Manu et Mente/Import symbolic pieces %#i")]
    public static void Import()
    {
        if(EditorApplication.isPlaying){Debug.LogWarning("Stop Play Mode before importing piece sprites.");return;}
        try
        {
            var source=new Texture2D(2,2,TextureFormat.RGBA32,false);
            if(!ImageConversion.LoadImage(source,File.ReadAllBytes("Design/PieceSprites/symbolic-transparent-source.png")))throw new Exception("Cannot read sprite artwork");
            if(source.width!=1536||source.height!=1024)throw new Exception("Unexpected atlas dimensions");
            var pixels=source.GetPixels32();Directory.CreateDirectory(Output);
            string[] types={"hand","mind","tech","app","schol","wise"};
            string[] oldGreen={"da9164c953d4f4c41863d355e1885c3d","f2d91e86794164c549a9c6111a902fb2","904e36d22c20748c6951723d7810b498","650a753197f764a109dc4081cb811664","1b84dd7b803234b3386244f166fc895c","26eb1f46c5cf841bfaeb5d5654f0b580"};
            string[] oldOrange={"dc7188c79089545d5ac2fd2fb6ed5d8f","6a403d35b675e4d9fa3c54b086a0fc8b","db81144bf3432428fbd7ff045d3e973e","d3384db11197e4a40969f9e600f8fe08","2044f0984087647c4910ddf07c169d59","4be235f8c6a6e4ae1b98097b4ecf08f6"};
            int[] left={24,272,520,768,1024,1274},right={272,512,768,1016,1274,1528};
            var replacements=new Dictionary<string,string>();
            for(int type=0;type<6;type++)for(int team=0;team<2;team++)
            {
                int column=(type%3)*2+team;
                int yMin=type<3?source.height-390:source.height-840;
                int yMax=type<3?source.height-64:source.height-520;
                int minX=right[column],minY=yMax,maxX=left[column],maxY=yMin,count=0;
                for(int y=yMin;y<yMax;y++)for(int x=left[column];x<right[column];x++)
                    if(pixels[y*source.width+x].a>128){minX=Math.Min(minX,x);minY=Math.Min(minY,y);maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y);count++;}
                if(count<1000)throw new Exception("Empty sprite cell: "+types[type]);
                // Include the soft antialiased boundary and pad with transparent pixels.
                minX=Math.Max(left[column],minX-3);minY=Math.Max(yMin,minY-3);
                maxX=Math.Min(right[column]-1,maxX+3);maxY=Math.Min(yMax-1,maxY+3);
                int w=maxX-minX+1,h=maxY-minY+1,size=Math.Max(w,h)+16;
                var output=new Texture2D(size,size,TextureFormat.RGBA32,false);
                var data=new Color32[size*size];int ox=(size-w)/2,oy=(size-h)/2;
                for(int y=0;y<h;y++)for(int x=0;x<w;x++)data[(y+oy)*size+x+ox]=pixels[(minY+y)*source.width+minX+x];
                output.SetPixels32(data);output.Apply();
                string path=Output+"/"+types[type]+(team==0?"_o":"_g")+".png";
                File.WriteAllBytes(path,output.EncodeToPNG());UnityEngine.Object.DestroyImmediate(output);
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
                importer.spritePixelsPerUnit=size/PieceDiameter;importer.alphaIsTransparency=true;
                importer.mipmapEnabled=false;importer.filterMode=FilterMode.Bilinear;importer.wrapMode=TextureWrapMode.Clamp;
                importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=512;
                var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
                var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if(sprite==null||Mathf.Abs(sprite.bounds.size.x-PieceDiameter)>.01f)throw new Exception("Invalid sprite size: "+path);
                replacements[team==0?oldOrange[type]:oldGreen[type]]=AssetDatabase.AssetPathToGUID(path);
            }
            UnityEngine.Object.DestroyImmediate(source);
            var targets=new List<string>(Directory.GetFiles("Assets/Scenes","*.unity"));targets.Add("Assets/Objects/piecePrefab.prefab");
            foreach(string path in targets)
            {
                string original=File.ReadAllText(path),updated=original;
                foreach(var pair in replacements)updated=updated.Replace(pair.Key,pair.Value);
                if(updated!=original)File.WriteAllText(path,updated);
            }
            AssetDatabase.Refresh();
            File.WriteAllText("/tmp/manu-sprite-import.txt","PASS: twelve transparent PNG sprites imported at consistent bounds; prefab and scene sprite references replaced. Pawns retained.");
            Debug.Log("Symbolic piece sprites imported successfully.");
        }
        catch(Exception e){File.WriteAllText("/tmp/manu-sprite-import.txt","FAIL: "+e);Debug.LogException(e);}
    }
}
#endif
