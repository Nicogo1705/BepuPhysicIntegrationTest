﻿using DotRecast.Detour;
using DotRecast.Recast.Toolset;
using Stride.BepuPhysics.Navigation.Components;
using Stride.Core.Annotations;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Games;
using Stride.Graphics;
using Stride.Rendering;
using Stride.Rendering.Materials.ComputeColors;
using Stride.Rendering.Materials;
using Stride.Core;
using Stride.Input;
using Stride.Engine.Processors;
using Stride.BepuPhysics.Processors;

namespace Stride.BepuPhysics.Navigation.Processors;
public class RecastMeshProcessor : EntityProcessor<BepuNavigationBoundingBoxComponent>
{

	public List<Vector3> Points = new List<Vector3>();
	public List<int> Indices = new List<int>();

	public DtNavMesh? NavMesh => _navMesh;

	private StrideNavMeshBuilder _navMeshBuilder = new();
	private RcNavMeshBuildSettings _navSettings = new();
	private DtNavMesh? _navMesh;
	private List<BepuNavigationBoundingBoxComponent> _boundingBoxes = new();
	private IGame _game;
	private SceneSystem _sceneSystem;
	private InputManager _input;
	private BepuStaticColliderProcessor _colliderProcessor = new();
	private ScriptSystem _scriptSystem;

	public RecastMeshProcessor()
	{
		// this is done to ensure that this processor runs after the BepuPhysicsProcessors
		Order = 20000;
	}

	protected override void OnSystemAdd()
	{
		_game = Services.GetService<IGame>();
		_sceneSystem = Services.GetService<SceneSystem>();
		_input = Services.GetSafeServiceAs<InputManager>();
		_sceneSystem.SceneInstance.Processors.Add(_colliderProcessor);

		_scriptSystem = Services.GetSafeServiceAs<ScriptSystem>();

		_sceneSystem.SceneInstance.RootSceneChanged += SceneInstance_RootSceneChanged;
		
		UpdateMeshData();
		// This locks the game for a second and needs to be fixed if dynamic navmeshes are to be used.
		CreateNavMesh();
	}

	private void SceneInstance_RootSceneChanged(object? sender, EventArgs e)
	{
		_colliderProcessor.BodyShapes.Clear();
		Dispose();
		_sceneSystem.SceneInstance.Processors.Add(_colliderProcessor);
		UpdateMeshData();
		// This locks the game for a second and needs to be fixed if dynamic navmeshes are to be used.
		CreateNavMesh();
	}

	protected override void OnEntityComponentAdding(Entity entity, [NotNull] BepuNavigationBoundingBoxComponent component, [NotNull] BepuNavigationBoundingBoxComponent data)
	{
		_boundingBoxes.Add(data);
	}

	protected override void OnEntityComponentRemoved(Entity entity, [NotNull] BepuNavigationBoundingBoxComponent component, [NotNull] BepuNavigationBoundingBoxComponent data)
	{
		_boundingBoxes.Remove(data);
	}

	public override void Update(GameTime time)
	{
		if (_input.IsKeyPressed(Keys.Space))
		{
			UpdateMeshData();
			// This locks the game for a second and needs to be fixed if dynamic navmeshes are to be used.
			CreateNavMesh();
		}
	}

	public void CreateNavMesh()
	{
		if(Points.Count == 0 || Indices.Count == 0)
		{
			return;
		}

		List<float> verts = new();
		// dotrecast wants a list of floats, so we need to convert the list of vectors to a list of floats
		// this may be able to be changed in the StrideGeomProvider class
		foreach (var v in Points)
		{
			verts.Add(v.X);
			verts.Add(v.Y);
			verts.Add(v.Z);
		}
		StrideGeomProvider geom = new StrideGeomProvider(verts, Indices);
		var result = _navMeshBuilder.Build(geom, _navSettings);

		_navMesh = result.NavMesh;

		var tileCount = _navMesh.GetTileCount();
		var tiles = new List<DtMeshTile>();
		for (int i = 0; i < tileCount; i++)
		{
			tiles.Add(_navMesh.GetTile(i));
		}

		List<Vector3> strideVerts = new List<Vector3>();
		
		// TODO: this is just me debugging should remove later
		//for (int i = 0; i < tiles.Count; i++)
		//{
		//	for (int j = 0; j < tiles[i].data.verts.Count();)
		//	{
		//		strideVerts.Add(
		//			new Vector3(tiles[i].data.verts[j++], tiles[i].data.verts[j++], tiles[i].data.verts[j++])
		//			);
		//	}
		//}
		//SpawPrefabAtVerts(strideVerts);
	}

	// TODO: this is just me debugging should remove later
	private void SpawPrefabAtVerts(List<Vector3> verts)
	{
		// Make sure the cube is a root asset or else this wont load
		var cube = _game.Content.Load<Model>("Cube");

		foreach (var vert in verts)
		{
			AddMesh(_game.GraphicsDevice, _sceneSystem.SceneInstance.RootScene, vert, cube.Meshes[0].Draw);
		}
	}

	// TODO: this is just me debugging should remove later
	Entity AddMesh(GraphicsDevice graphicsDevice, Scene rootScene, Vector3 position, MeshDraw meshDraw)
	{
		var entity = new Entity { Scene = rootScene, Transform = { Position = position } };
		var model = new Model
		{
		new MaterialInstance
		{
			Material = Material.New(graphicsDevice, new MaterialDescriptor
			{
				Attributes = new MaterialAttributes
				{
					DiffuseModel = new MaterialDiffuseLambertModelFeature(),
					Diffuse = new MaterialDiffuseMapFeature
					{
						DiffuseMap = new ComputeVertexStreamColor()
					},
				}
			})
		},
		new Mesh
		{
			Draw = meshDraw,
			MaterialIndex = 0
		}
		};
		entity.Add(new ModelComponent { Model = model });
		return entity;
	}

	/// <summary>
	/// Gets all the points and indices from the BepuStaticColliderProcessor and adds them to the mesh data.
	/// </summary>
	private void UpdateMeshData()
	{
		Points.Clear();
		Indices.Clear();

		foreach (var shape in _colliderProcessor.BodyShapes)
		{
			AppendArrays(shape.Value.Points, shape.Value.Indices);
		}
	}

	public void AppendArrays(List<Vector3> vertices, List<int> indices)
	{
		// Copy vertices
		int vbase = Points.Count;
		for (int i = 0; i < vertices.Count; i++)
		{
			Points.Add(vertices[i]);
		}

		// Copy indices with offset applied
		foreach (int index in indices)
		{
			Indices.Add(index + vbase);
		}
	}

	protected override void OnSystemRemove()
	{
		Dispose();
	}

	private void Dispose()
	{
		_sceneSystem.SceneInstance.Processors.Remove(_colliderProcessor);
	}
}
