using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System;
using System.Collections;
using System.Threading;
using System.Text;
/*
        - premik po asinhronih taskih
        - pool za asinhrone taske
        - 3 nivoji
        - piramida 1:9
        - 9+3+3 terenov na nivo 
        - omejitev indeksov pri premikanju raycasta (zmanjsaj za ena-> boljse naredi splosno)
        - koda se izvaja glede na aplikacijo-> ne podajaj absolutne poti
        - povecaj resolucijo terana na 1025
        - pridobivanje teksut iz get url
        - dodaj najvecji nivo v assets, ce lahko
        - BUGS random index bug + spremijanje teksutre na belo ?
         */



public class Main : MonoBehaviour
{
    private GameObject pointer;

    private List<List<GameObject>> levelsTilesPool = new List<List<GameObject>>();
    //private float[] levelOffsets = { 50f, 45f, 35f, 25f, 0f };
    //private float[] levelSizes = { 32f, 128f, 512f, 2048f, 8192f };
    //private int[] levelHeightmapSizes = { 32, 64, 64, 64, 256 };
    //private int[] levelHeightmapsStrides = { 1, 2, 8, 32, 32 };
    private string[] levelsHeightmapNames = { "A/" ,"B/","C/","D/","E/" };

    //private float[] zoomLimits = { 4096f, 1024f, 512f, 1f };

    //private string[] levelColors = { "red", "green", "blue", "yellow", "cyan" };
    //private List<int[]> zoomLevels = new List<int[]>();
    //private List<int[]> zoomLevelPos = new List<int[]>();
    //private Vector3 staraPozicijaKamere = new Vector3();
    private List<List<Texture2D>> textureHolder = new List<List<UnityEngine.Texture2D>>();

    private List<List<GameObject>> nivoji = new List<List<GameObject>>();
    private Texture2D tmpTekstura = null;
    private Texture2D tmpTekstura2 = null;
    int velikostNivojaMax = 32;
    Vector3 oldCameraPosition = new Vector3();
    int levels = 5;

    //  Start - Initialising all the structures. 
    void Start()
    {

        print("START");
        tmpTekstura = LoadImageTexture(128, "Assets/Resources/textures/green.png");
        tmpTekstura2 = LoadImageTexture(128, "Assets/Resources/textures/red.png");


        for (int j = 0; j < levels; j++)
        {
            nivoji.Add(new List<GameObject>());
            textureHolder.Add(new List<Texture2D>());
            for (int i = 0; i < 4; i++)
            {
                GameObject terrainNivoD = MyCreateTerrain(velikostNivojaMax, velikostNivojaMax, new Vector3(0, 0, 0), tmpTekstura2, "Terrain-" + (j + 1));
                nivoji[j].Add(terrainNivoD);
                textureHolder[j].Add(tmpTekstura);
            }
            velikostNivojaMax *= 2;
        }

        //  Reading Heightmap
        ReadHeightmapFile();

        //  Initialise terrain tiles pool
        for (int i = 0; i < 3; i++)
            levelsTilesPool.Add(new List<GameObject>());
        levelsTilesPool.Add(new List<GameObject>());
        levelsTilesPool[5].Add(CreateBaseTerrain());

        pointer = GameObject.Find("Sphere");
       

        //  Pointer position
        if (Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out hit))
        {
            pointer.transform.position = new Vector3(hit.point.x, 1000f, hit.point.z);
            oldCameraPosition = Camera.main.transform.position;
        }

        


    }



    //private int zoomLevel = 0;
    void Update()
    {
        //  Stopwatch s = Stopwatch.StartNew();
        UpdateView();
        if (Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out hit) &&
            oldCameraPosition != Camera.main.transform.position)
        {

            pointer.transform.position = new Vector3(hit.point.x, terrainHeight, hit.point.z);
           
            updateMap();
        }
        oldCameraPosition = Camera.main.transform.position;



    }

    private float maxTerrainHeight = 2900.0f;   // Max height
    private float terrainHeight = 500.0f;     // Height of highest part of terrain in world dimensions



    //  Method for creating the ingame terrain with setting initial heights for all the terrains 
    GameObject CreateBaseTerrain()
    {
        int size = 8192;
        int heightmapResolution = 1024;
        int stride = size/ heightmapResolution;

        TerrainData terrainData = new TerrainData();
        terrainData.heightmapResolution = heightmapResolution;
        terrainData.baseMapResolution = heightmapResolution / 4;
        terrainData.SetDetailResolution(1024, 16);

        float[,] heights = new float[heightmapResolution + 1, heightmapResolution + 1];
        for (int i = 0; i < heightmapResolution + 1; i++)
            for (int j = 0; j < heightmapResolution + 1; j++)
            {
                int hi =   i * stride;
                int hj =  j * stride;
                hi = (hi >= heightmapData.GetLength(0)) ? heightmapData.GetLength(0) - 1 : hi;
                hj = (hj >= heightmapData.GetLength(1)) ? heightmapData.GetLength(1) - 1 : hj;
                heights[i, j] = heightmapData[hi, hj] / maxTerrainHeight;
            }

        terrainData.SetHeights(0, 0, heights);
        terrainData.size = new Vector3(size, terrainHeight, size);
        GameObject terrainGameObject = Terrain.CreateTerrainGameObject(terrainData);
        terrainGameObject.transform.position = new Vector3(0, 0, 0);
        Terrain terrain = terrainGameObject.GetComponent<Terrain>();
        terrain.castShadows = false;
        terrain.drawTreesAndFoliage = false;


        // Texturing part
        Texture2D colorTexture = CreateHeightmapColorTexture(heightmapResolution, heights);
        SplatPrototype splt = new SplatPrototype();
        splt.texture = colorTexture;
        splt.tileOffset = new Vector2(0f, 0f);
        splt.tileSize = new Vector2(terrainData.size.x, terrainData.size.z);
        SplatPrototype[] splts = new SplatPrototype[1];
        splts[0] = splt;
        terrainData.splatPrototypes = splts;
        terrain.terrainData.RefreshPrototypes();

        terrainGameObject.name = "Terrain_" + "BASE";
        terrain.Flush();
        return terrainGameObject;
    }



    RaycastHit hit;
    private static float moveSpeed = 0.01f;
    private static float rotateSpeed = 0.5f;
    private static float scrollSpeed = 0.05f;
    Vector3 cameraPivot = new Vector3(0f, 0f, 0f);
    float scroll = 0f;

    // posodabljanje pogleda glede na vnos uporabnika
    void UpdateView()
    {
        //  View reset command
        if (Input.GetKeyDown(KeyCode.R))
        {
            Camera.main.transform.position = new Vector3(4096, 8192, 4096);
            Camera.main.transform.rotation = Quaternion.identity;
            Camera.main.transform.Rotate(90, 0, 0);
        }

        //  View move (Up - W, Left - A, Down - S, Right - D)
        if (Input.GetKey(KeyCode.W) )
        {
            Camera.main.transform.Translate(0.0f, 0.0f, Camera.main.transform.position.y * moveSpeed, Space.World);
        }
        if (Input.GetKey(KeyCode.A) )
        {
            Camera.main.transform.Translate(Camera.main.transform.position.y * -moveSpeed, 0.0f, 0.0f, Space.World);
        }
        if (Input.GetKey(KeyCode.S))
        {
            Camera.main.transform.Translate(0.0f, 0.0f, Camera.main.transform.position.y * -moveSpeed, Space.World);
        }
        if (Input.GetKey(KeyCode.D))
        {
            Camera.main.transform.Translate(Camera.main.transform.position.y * moveSpeed, 0.0f, 0.0f, Space.World);
        }

        //  View tilt (Down - E, Up - Q)
        cameraPivot.x = Camera.main.transform.position.x;
        cameraPivot.z = Camera.main.transform.position.z;
        if (Input.GetKey(KeyCode.Q) && Camera.main.transform.eulerAngles.x <= 89f && Camera.main.transform.eulerAngles.y == 0)
        {
            Camera.main.transform.RotateAround(cameraPivot, Camera.main.transform.right, rotateSpeed);
        }
        if (Input.GetKey(KeyCode.E) && Camera.main.transform.eulerAngles.x > 1f)
        {
            Camera.main.transform.RotateAround(cameraPivot, Camera.main.transform.right, -rotateSpeed);
        }

        //  View zoom (Zoom In - Scroll Up, Zoom Out - Scrool Down) 
        scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll > 0 && Camera.main.transform.position.y > 100f)
        {
            Camera.main.transform.Translate(0.0f, 0.0f, Camera.main.transform.position.y * scrollSpeed, Space.Self);
        }
        else if (scroll < 0 && Camera.main.transform.position.y < 8192f)
        {
            Camera.main.transform.Translate(0.0f, 0.0f, Camera.main.transform.position.y * -scrollSpeed, Space.Self);
        }
    }

    private float[,] heightmapColorData =   {{0f, 1.0000f, 1.0000f},
                                            {0.0105f, 0.4188f, 0.2019f},
                                            {0.0219f, 0.4375f, 0.2037f},
                                            {0.0342f, 0.4562f, 0.2057f},
                                            {0.0475f, 0.4750f, 0.2078f},
                                            {0.0617f, 0.4938f, 0.2102f},
                                            {0.0769f, 0.5125f, 0.2130f},
                                            {0.0930f, 0.5313f, 0.2162f},
                                            {0.1100f, 0.5500f, 0.2200f},
                                            {0.1280f, 0.5687f, 0.2244f},
                                            {0.1469f, 0.5875f, 0.2295f},
                                            {0.1667f, 0.6062f, 0.2354f},
                                            {0.1875f, 0.6250f, 0.2422f},
                                            {0.2092f, 0.6438f, 0.2500f},
                                            {0.2319f, 0.6625f, 0.2588f},
                                            {0.2555f, 0.6813f, 0.2688f},
                                            {0.2800f, 0.7000f, 0.2800f},
                                            {0.3184f, 0.7188f, 0.3055f},
                                            {0.3572f, 0.7375f, 0.3319f},
                                            {0.3964f, 0.7562f, 0.3592f},
                                            {0.4359f, 0.7750f, 0.3875f},
                                            {0.4756f, 0.7937f, 0.4167f},
                                            {0.5154f, 0.8125f, 0.4469f},
                                            {0.5552f, 0.8313f, 0.4780f},
                                            {0.5950f, 0.8500f, 0.5100f},
                                            {0.6346f, 0.8688f, 0.5430f},
                                            {0.6739f, 0.8875f, 0.5769f},
                                            {0.7130f, 0.9063f, 0.6117f},
                                            {0.7516f, 0.9250f, 0.6475f},
                                            {0.7897f, 0.9437f, 0.6842f},
                                            {0.8271f, 0.9625f, 0.7219f},
                                            {0.8640f, 0.9812f, 0.7605f},
                                            {0.9000f, 1.0000f, 0.8000f},
                                            {0.8777f, 0.9806f, 0.7592f},
                                            {0.8573f, 0.9613f, 0.7194f},
                                            {0.8387f, 0.9419f, 0.6806f},
                                            {0.8218f, 0.9226f, 0.6428f},
                                            {0.8066f, 0.9032f, 0.6060f},
                                            {0.7928f, 0.8839f, 0.5702f},
                                            {0.7805f, 0.8645f, 0.5354f},
                                            {0.7694f, 0.8452f, 0.5016f},
                                            {0.7596f, 0.8258f, 0.4688f},
                                            {0.7508f, 0.8065f, 0.4370f},
                                            {0.7431f, 0.7871f, 0.4062f},
                                            {0.7362f, 0.7677f, 0.3764f},
                                            {0.7301f, 0.7484f, 0.3476f},
                                            {0.7246f, 0.7290f, 0.3198f},
                                            {0.7097f, 0.6996f, 0.2930f},
                                            {0.6903f, 0.6653f, 0.2672f},
                                            {0.6710f, 0.6306f, 0.2424f},
                                            {0.6516f, 0.5957f, 0.2186f},
                                            {0.6323f, 0.5607f, 0.1958f},
                                            {0.6129f, 0.5256f, 0.1740f},
                                            {0.5935f, 0.4906f, 0.1532f},
                                            {0.5742f, 0.4557f, 0.1334f},
                                            {0.5548f, 0.4211f, 0.1145f},
                                            {0.5355f, 0.3869f, 0.0967f},
                                            {0.5161f, 0.3531f, 0.0799f},
                                            {0.4968f, 0.3200f, 0.0641f},
                                            {0.4774f, 0.2875f, 0.0493f},
                                            {0.4581f, 0.2559f, 0.0355f},
                                            {0.4387f, 0.2251f, 0.0226f},
                                            {0.4194f, 0.1953f, 0.0108f},
                                            {0.4000f, 0.1667f,  0f}};

    private Texture2D CreateHeightmapColorTexture(int size, float [,] heights)
    {
        int heightSize = heights.GetLength(0)-1;
        Texture2D heightmapColorTexture = new Texture2D(heightSize, heightSize, TextureFormat.ARGB32, false);

        for (int i = 0; i < size; i++)
        {
            for (int j = 0; j < size; j++)
            {
                

                int index = (int)(heights[i, j] * heightmapColorData.GetLength(0));
                Color c = new Color(heightmapColorData[index, 0], heightmapColorData[index, 1], heightmapColorData[index, 2]);
                heightmapColorTexture.SetPixel(j, i, c);
            }
        }
       

        return heightmapColorTexture;

    }

    private float getAvg(float [,] list) {
        int weight = list.GetLength(0) * list.GetLength(1);

        float avg = 0;
        for (int i = 0; i < list.GetLength(0); i++) {
            for (int j = 0; j < list.GetLength(1); j++) {
                avg += list[i,j] / weight;
            }
        }

        return avg;

    }


    private float[,] heightmapData = new float[8192, 8192];

    // metoda za branje osnovih višinskih podatkov
    private void ReadHeightmapFile()
    {
       

            byte[] fileData;
            String filePath = @"./Assets/Resources/heightmaps/baseHeightMap.png";
            if (File.Exists(filePath))
            {
                fileData = File.ReadAllBytes(filePath);
                Texture2D tex = new Texture2D(8192, 8192);
                tex.LoadImage(fileData);
                print("Reading...");
                for (int i = 0; i < 8192; i++)
                    for (int j = 0; j < 8192; j++)
                        heightmapData[j, i] = tex.GetPixel(i, j).r * maxTerrainHeight;
            }
            else
            {
                print("Error reading heightmap data.");
            }

    }

    

    GameObject MyCreateTerrain(int heightmapResolution, int size, Vector3 position, Texture2D texture, String name)
    {
        TerrainData terrainData = new TerrainData();
        terrainData.heightmapResolution = heightmapResolution;
        terrainData.baseMapResolution = heightmapResolution / 4;
        terrainData.SetDetailResolution(1024, 16);

        float[,] heights = new float[heightmapResolution + 1, heightmapResolution + 1];
        for (int i = 0; i < heightmapResolution + 1; i++)
            for (int j = 0; j < heightmapResolution + 1; j++)
            {
                int hi = i + (int)position.z;
                int hj = j + (int)position.x;
                if (0 <= hj && hj < heightmapData.GetLength(0) && 0 <= hi && hi < heightmapData.GetLength(1))
                    heights[i, j] = heightmapData[hi, hj] / maxTerrainHeight;
                else
                    heights[i, j] = 0;
            }
        terrainData.SetHeights(0, 0, heights);
        terrainData.size = new Vector3(size, terrainHeight, size);
        GameObject terrainGameObject = Terrain.CreateTerrainGameObject(terrainData);
        terrainGameObject.transform.position = position;
        Terrain terrain = terrainGameObject.GetComponent<Terrain>();
        terrain.castShadows = false;
        terrain.drawTreesAndFoliage = false;

       
        SplatPrototype splt = new SplatPrototype();
        splt.texture = texture;
        splt.tileOffset = new Vector2(0f, 0f);
        splt.tileSize = new Vector2(terrainData.size.x, terrainData.size.z);
        SplatPrototype[] splts = new SplatPrototype[1];
        splts[0] = splt;
        terrainData.splatPrototypes = splts;
        terrain.terrainData.RefreshPrototypes();
        terrainGameObject.name = name;
        

       
        terrain.Flush();
        return terrainGameObject;
    }




    //odpiranje tekstur sinhrono-slabost pocasno
    private Texture2D LoadImageTexture(int size, string filePath)
    {
        byte[] fileData;
        Texture2D heightmapImageTexture = new Texture2D(size, size, TextureFormat.ARGB32, false); ;
        if (File.Exists(filePath))
        {
            fileData = File.ReadAllBytes(filePath);
            heightmapImageTexture.LoadImage(fileData);
        }
        return heightmapImageTexture;

    }

    
    Vector2[] positions = new Vector2[5];
    Texture2D heightMapTexture;
    String RELATIVE_PATH = @"E:/Levels/";
    void updateMap()
    {
        Vector3 hit = GameObject.Find("Sphere").transform.position;
        Vector2 position;
        int sirina = 245;
        int visina = 162;
        int velikost = 32;
        Vector2[] newPositions = new Vector2[levels];

        //dolocimo indexe za visinke slike
       for (int i = 0; i < levels; i++)
        {
            position.x = (int)(hit.x+(velikost/2))/ velikost;
            if (position.x < 0)
                position.x = 0;
            else if (position.x > sirina - 2)
                position.x = sirina - 2;

            position.y = (int)(8192 - hit.z-velikost/2) / velikost;
            if (position.y < 0)
                position.y = 0;
            else if (position.y > visina - 2)
                position.y = visina - 2;

            sirina = (int)(sirina+1 )/ 2 ;
            visina = (int)(visina + 1 )/ 2;
            velikost *=2;
            newPositions[i] = position;
            
        }

        velikost = 32;
 
        //doloèimo višinske podatke in pozicijo za terrain
        String path,path1;
        Vector3 newPosition;
        
        for (int i = 0; i < 1; i++) {
            if (newPositions[i] != positions[i])
            {
                path = RELATIVE_PATH+levelsHeightmapNames[i];
                print("NIVO: "+i);
                int size = 1024/((i+1)*2);

                path1 = path + newPositions[i].y+ "_"+ newPositions[i].x+".png";
                newPosition.x = newPositions[i].x * velikost-velikost;
                newPosition.z = 8192  - newPositions[i].y * velikost - velikost;
                newPosition.y = 30 * (i + 1);

                MyUpdateTerrain(nivoji[i][0], newPosition , path1);

                path1 = path + (newPositions[i].y+1) + "_" + newPositions[i].x + ".png";
                newPosition.x = newPositions[i].x * velikost - velikost;
                newPosition.z = 8192 - (newPositions[i].y + 1) * velikost - velikost;
                newPosition.y = 30 * (i + 1);
                MyUpdateTerrain(nivoji[i][1], newPosition, path1);

                path1 = path + newPositions[i].y + "_" + (newPositions[i].x+1) + ".png";
                newPosition.x = (newPositions[i].x + 1) * velikost - velikost;
                newPosition.z = 8192 - newPositions[i].y * velikost - velikost;
                newPosition.y = 30 * (i + 1);
                MyUpdateTerrain(nivoji[i][2], newPosition, path1);

                path1 = path + (newPositions[i].y + 1) + "_" + (newPositions[i].x + 1) + ".png";
                newPosition.x = (newPositions[i].x+1) * velikost - velikost;
                newPosition.z = 8192  - (newPositions[i].y+1) * velikost - velikost;
                newPosition.y = 30 * (i + 1);
                MyUpdateTerrain(nivoji[i][3], newPosition, path1);



                velikost *= 2;
            }
            else
                break;
        }
        positions = newPositions;
    }

    void MyUpdateTerrain(GameObject terrainGameObject, Vector3 newPosition, String pathHeightMap)
    {

        //premik terena
        terrainGameObject.transform.position = newPosition;

        //TODO spremeni visinske podatke iz osnovnega nivoja
        

        //spremenimo teksturo terena
        StartCoroutine(changeTexture(terrainGameObject, pathHeightMap));


        //spremenimo visinske podatke terena
        IEnumerator i=changeHeightData(terrainGameObject, pathHeightMap);
        StartCoroutine(i);





       

    }



    IEnumerator changeTexture(GameObject terrainGameObject,String url) {
        //TODO remove for url
        WWW www = new WWW("File://"+url);

        yield return www;

        Texture2D tex = www.texture;
        /*Texture2D coloredTex= www.texture;

        for (int i = 0; i < 1024; i++)
        {
            for (int j = 0; j < 1024; j++)
            {
                int index = (int)(tex.GetPixel(i, j).r  * heightmapColorData.GetLength(0));
                Color c = new Color(heightmapColorData[index, 0], heightmapColorData[index, 1], heightmapColorData[index, 2]);
                coloredTex.SetPixel(j, i, c);
                

            }
        }
        */


        Terrain terrain = terrainGameObject.GetComponent<Terrain>();
        
        TerrainData terrainData = terrain.terrainData;
        SplatPrototype[] splts = terrainData.splatPrototypes;
        SplatPrototype splt = splts[0];

        splt.texture = tex;//coloredTex;
        terrainData.splatPrototypes = splts;
        terrain.terrainData.RefreshPrototypes();
        terrain.Flush();
       
    }


    IEnumerator changeHeightData(GameObject terrainGameObject, String url)
    {
        //TODO remove "File://" for url
        WWW www = new WWW("File://" + url);

        yield return www;


        //int size =(int) terrainGameObject.GetComponent<Collider>().bounds.size.x;
        
     
        Terrain terrain = terrainGameObject.GetComponent<Terrain>();
        TerrainData terrainData = terrain.terrainData;

        int heightmapResolution = terrain.terrainData.heightmapResolution-1;
        int stride = 1024 / heightmapResolution;

        float[,] heightmapDataImage = new float[1024,1024];
        Texture2D tex = www.texture;
        
        for (int i = 0; i < 1024; i++)
            for (int j = 0; j < 1024; j++)
                heightmapDataImage[j, i] = tex.GetPixel(i, j).r * maxTerrainHeight;

        float[,] heights = new float[heightmapResolution + 1, heightmapResolution + 1];
        for (int i = 0; i < heightmapResolution + 1; i++)
            for (int j = 0; j < heightmapResolution + 1; j++)
            {
                int hi = i * stride;
                int hj = j * stride;
                hi = (hi >= heightmapDataImage.GetLength(0)) ? heightmapDataImage.GetLength(0) - 1 : hi;
                hj = (hj >= heightmapDataImage.GetLength(1)) ? heightmapDataImage.GetLength(1) - 1 : hj;
                heights[i, j] = heightmapDataImage[hi, hj] / maxTerrainHeight;
            }

        terrain.terrainData.SetHeights(0, 0, heights);
        


   
        terrain.Flush();

    }

}


