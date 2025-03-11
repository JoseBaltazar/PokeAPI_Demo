using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Clase encargada de generar niveles procedurales con suelo, paredes, obstáculos, items y Pokémon
/// </summary>
public class LevelCreator : MonoBehaviour
{
    [Header("Level Settings")]
    /// <summary>Ancho del nivel en unidades</summary>
    public int width = 50;
    /// <summary>Largo del nivel en unidades</summary>
    public int length = 50;
    /// <summary>Altura máxima de los obstáculos</summary>
    public int maxHeight = 3;
    /// <summary>Tamaño de cada unidad/celda del nivel</summary>
    public float roomSize = 1f;

    [Header("Prefabs")]
    /// <summary>Prefab para el suelo del nivel</summary>
    public GameObject floorPrefab;
    /// <summary>Prefab para las paredes</summary>
    public GameObject wallPrefab;
    /// <summary>Prefab para el techo</summary>
    public GameObject ceilingPrefab;
    /// <summary>Array de prefabs para obstáculos aleatorios</summary>
    public GameObject[] obstaclePrefabs;

    [Header("Generation Settings")]
    /// <summary>Porcentaje de probabilidad de generar obstáculos</summary>
    [Range(0, 100)]
    public int obstaclePercentage = 20;
    /// <summary>Determina si se genera techo en el nivel</summary>
    public bool generateCeiling = true;

    [Header("Items Settings")]
    /// <summary>Porcentaje de probabilidad de generar items</summary>
    [Range(0, 100)]
    public int itemSpawnPercentage = 10;
    /// <summary>Array de prefabs de items para spawneo</summary>
    public GameObject[] itemPrefabs;
    /// <summary>Distancia mínima entre items</summary>
    public float minItemSpacing = 3f;

    [Header("Pokemon Settings")]
    /// <summary>Cantidad total de Pokémon a generar en el nivel</summary>
    public int totalPokemonToSpawn = 10;
    /// <summary>Array de prefabs de Pokémon disponibles</summary>
    public GameObject[] pokemonPrefabs;
    /// <summary>Distancia mínima entre Pokémon</summary>
    public float minPokemonSpacing = 5f;
    private int pokemonSpawned = 0;
    private List<int> usedPokemonIndices = new List<int>(); // Nueva lista para rastrear índices usados

    private List<Vector2> occupiedPositions = new List<Vector2>();

    // Sistema de grid para rastrear objetos
    private Dictionary<Vector2Int, List<GameObject>> gridObjects = new Dictionary<Vector2Int, List<GameObject>>();
    private Dictionary<Vector2Int, BlockType> gridBlockTypes = new Dictionary<Vector2Int, BlockType>();

    /// <summary>
    /// Enum que define los tipos de bloques posibles en el grid
    /// </summary>
    public enum BlockType
    {
        Empty,      // Espacio vacío
        Floor,      // Suelo
        Obstacle,   // Obstáculo
        Wall,       // Pared
        Item,       // Item
        Pokemon     // Pokémon
    }

    /// <summary>
    /// Inicializa la generación del nivel al comenzar
    /// </summary>
    private void Start()
    {
        GenerateLevel();
    }

    /// <summary>
    /// Método principal que coordina la generación completa del nivel
    /// Limpia el estado anterior y genera todos los elementos del nivel
    /// </summary>
    void GenerateLevel()
    {
        occupiedPositions.Clear();
        gridObjects.Clear();
        gridBlockTypes.Clear();
        pokemonSpawned = 0;
        usedPokemonIndices.Clear(); // Limpiar la lista de índices usados
        GameObject levelContainer = new GameObject("GeneratedLevel");
        levelContainer.transform.parent = transform;

        // Generar suelo y objetos
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < length; z++)
            {
                Vector3 position = new Vector3(x * roomSize, 0, z * roomSize);
                GameObject floor = Instantiate(floorPrefab, position, Quaternion.identity);
                floor.transform.parent = levelContainer.transform;
                
                AddToGrid(new Vector2Int(x, z), floor, BlockType.Floor);

                // Generar obstáculos
                if (Random.Range(0, 100) < obstaclePercentage)
                {
                    GenerateObstacle(x, z, levelContainer);
                    occupiedPositions.Add(new Vector2(x, z));
                }
                // Intentar generar item si no hay obstáculo
                else if (Random.Range(0, 100) < itemSpawnPercentage)
                {
                    TrySpawnItem(x, z, levelContainer);
                }
                // Intentar generar Pokémon si aún no hemos alcanzado el límite
                else if (pokemonSpawned < totalPokemonToSpawn)
                {
                    TrySpawnPokemon(x, z, levelContainer);
                }
            }
        }

        // Generar paredes exteriores
        GenerateWalls(levelContainer);

        // Generar techo si está activado
        if (generateCeiling)
        {
            GenerateCeiling(levelContainer);
        }
    }

    /// <summary>
    /// Genera obstáculos en una posición específica con altura aleatoria
    /// </summary>
    /// <param name="x">Posición X en el grid</param>
    /// <param name="z">Posición Z en el grid</param>
    /// <param name="container">Contenedor padre para los obstáculos</param>
    private void GenerateObstacle(int x, int z, GameObject container)
    {
        float height = Random.Range(1, maxHeight);
        for (int y = 1; y <= height; y++)
        {
            Vector3 obstaclePos = new Vector3(x * roomSize, y * roomSize, z * roomSize);
            GameObject obstacle = Instantiate(
                obstaclePrefabs[Random.Range(0, obstaclePrefabs.Length)],
                obstaclePos,
                Quaternion.identity
            );
            obstacle.transform.parent = container.transform;
            AddToGrid(new Vector2Int(x, z), obstacle, BlockType.Obstacle);
        }
    }

    /// <summary>
    /// Intenta spawner un item en una posición válida
    /// </summary>
    /// <returns>True si el item fue spawneado exitosamente</returns>
    private bool TrySpawnItem(int x, int z, GameObject container)
    {
        // Verificar si hay espacio suficiente
        if (IsPositionValid(x, z, minItemSpacing))
        {
            Vector3 itemPosition = new Vector3(x * roomSize, roomSize * 1.8f, z * roomSize);
            GameObject item = Instantiate(
                itemPrefabs[Random.Range(0, itemPrefabs.Length)],
                itemPosition,
                Quaternion.identity
            );
            item.transform.parent = container.transform;
            occupiedPositions.Add(new Vector2(x, z));
            AddToGrid(new Vector2Int(x, z), item, BlockType.Item);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Intenta spawner un Pokémon en una posición válida
    /// Asegura que no se repitan los Pokémon usando índices únicos
    /// </summary>
    /// <returns>True si el Pokémon fue spawneado exitosamente</returns>
    private bool TrySpawnPokemon(int x, int z, GameObject container)
    {
        // Verificar si aún hay Pokémon disponibles para spawn
        if (usedPokemonIndices.Count >= pokemonPrefabs.Length)
            return false;

        if (IsPositionValid(x, z, minPokemonSpacing))
        {
            // Obtener un índice aleatorio no usado
            int pokemonIndex;
            do
            {
                pokemonIndex = Random.Range(0, pokemonPrefabs.Length);
            } while (usedPokemonIndices.Contains(pokemonIndex));

            usedPokemonIndices.Add(pokemonIndex); // Marcar el índice como usado

            Vector3 pokemonPosition = new Vector3(x * roomSize, 1.5f, z * roomSize);
            GameObject pokemon = Instantiate(
                pokemonPrefabs[pokemonIndex],
                pokemonPosition,
                Quaternion.identity
            );
            pokemon.transform.parent = container.transform;
            occupiedPositions.Add(new Vector2(x, z));
            AddToGrid(new Vector2Int(x, z), pokemon, BlockType.Pokemon);
            pokemonSpawned++;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Sistema de grid que registra objetos y sus tipos en posiciones específicas
    /// </summary>
    private void AddToGrid(Vector2Int gridPosition, GameObject obj, BlockType type)
    {
        if (!gridObjects.ContainsKey(gridPosition))
        {
            gridObjects[gridPosition] = new List<GameObject>();
        }
        gridObjects[gridPosition].Add(obj);
        gridBlockTypes[gridPosition] = type;
    }

    // Método para obtener objetos en una posición específica
    public List<GameObject> GetObjectsAt(Vector2Int position)
    {
        return gridObjects.ContainsKey(position) ? gridObjects[position] : new List<GameObject>();
    }

    // Método para obtener el tipo de bloque en una posición
    public BlockType GetBlockTypeAt(Vector2Int position)
    {
        return gridBlockTypes.ContainsKey(position) ? gridBlockTypes[position] : BlockType.Empty;
    }

    // Método para obtener objetos en un radio específico
    public Dictionary<Vector2Int, List<GameObject>> GetObjectsInRadius(Vector2Int center, int radius)
    {
        Dictionary<Vector2Int, List<GameObject>> result = new Dictionary<Vector2Int, List<GameObject>>();
        
        for (int x = -radius; x <= radius; x++)
        {
            for (int z = -radius; x <= radius; z++)
            {
                Vector2Int checkPos = new Vector2Int(center.x + x, center.y + z);
                if (gridObjects.ContainsKey(checkPos))
                {
                    result[checkPos] = gridObjects[checkPos];
                }
            }
        }
        
        return result;
    }

    /// <summary>
    /// Verifica si una posición es válida para spawneo basándose en la distancia mínima
    /// </summary>
    /// <returns>True si la posición cumple con la distancia mínima requerida</returns>
    private bool IsPositionValid(int x, int z, float minSpacing)
    {
        foreach (Vector2 pos in occupiedPositions)
        {
            float distance = Vector2.Distance(new Vector2(x, z), pos);
            if (distance < minSpacing)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Genera las paredes exteriores del nivel
    /// </summary>
    void GenerateWalls(GameObject container)
    {
        // Paredes en X
        for (int x = -1; x <= width; x++)
        {
            for (int y = 0; y < maxHeight; y++)
            {
                Vector3 pos1 = new Vector3(x * roomSize, y * roomSize, -1 * roomSize);
                Vector3 pos2 = new Vector3(x * roomSize, y * roomSize, length * roomSize);
                
                Instantiate(wallPrefab, pos1, Quaternion.identity).transform.parent = container.transform;
                Instantiate(wallPrefab, pos2, Quaternion.identity).transform.parent = container.transform;
                AddToGrid(new Vector2Int(x, -1), wallPrefab, BlockType.Wall);
                AddToGrid(new Vector2Int(x, length), wallPrefab, BlockType.Wall);
            }
        }

        // Paredes en Z
        for (int z = -1; z <= length; z++)
        {
            for (int y = 0; y < maxHeight; y++)
            {
                Vector3 pos1 = new Vector3(-1 * roomSize, y * roomSize, z * roomSize);
                Vector3 pos2 = new Vector3(width * roomSize, y * roomSize, z * roomSize);
                
                Instantiate(wallPrefab, pos1, Quaternion.identity).transform.parent = container.transform;
                Instantiate(wallPrefab, pos2, Quaternion.identity).transform.parent = container.transform;
                AddToGrid(new Vector2Int(-1, z), wallPrefab, BlockType.Wall);
                AddToGrid(new Vector2Int(width, z), wallPrefab, BlockType.Wall);
            }
        }
    }

    /// <summary>
    /// Genera el techo del nivel si está activada la opción
    /// </summary>
    void GenerateCeiling(GameObject container)
    {
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < length; z++)
            {
                Vector3 position = new Vector3(x * roomSize, maxHeight * roomSize, z * roomSize);
                GameObject ceiling = Instantiate(ceilingPrefab, position, Quaternion.identity);
                ceiling.transform.parent = container.transform;
                AddToGrid(new Vector2Int(x, z), ceiling, BlockType.Empty);
            }
        }
    }
}
