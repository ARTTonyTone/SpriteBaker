using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.VisualScripting;
using UnityEngine;

[Serializable]
public struct TextureData
{
    public Vector2 canvasPos;
    [Header("Runtime Only")]
    public Color color;
    public Vector2 spriteOffset; 
    public Vector2 canvasSize;
}
[Serializable]
public struct SpriteData
{
    public string spriteName;
    public Sprite sprite;
    public Color color;
    public Vector2 canvasPos;
}

[ExecuteInEditMode]
public class SpriteBaker : MonoBehaviour
{
    [Header("Settings")]
    public List<SpriteData> _spriteCollection;
    private List<TextureData> _spriteDatas;
    [SerializeField] private ComputeShader _computeShader;

    private RenderTexture _renderTexture;
    private ComputeBuffer _dataBuffer;
    private MaterialPropertyBlock _propBlock;
    private Renderer _renderer;

    #region Basic funcs
    private void Start() {
        _computeShader = Resources.Load<ComputeShader>("TextureBaker");
        if (_computeShader != null)
        {
            Bake();
        }
    }
    private void OnValidate()
    {
        if (_computeShader != null)
        {
            Bake();
        }
    }

    private void OnDisable() => Cleanup();
    private void OnDestroy() => Cleanup();
    #endregion

    #region Private funcs
    public void Bake()
    {
        if (_computeShader == null) return;

        int kernel = _computeShader.FindKernel("CSMain");

        SetDatas();

        CaldulateTextureSize(out int width, out int height);

        if (width <= 0 || height <= 0) return;

        if (_renderTexture == null || _renderTexture.width != width || _renderTexture.height != height)
        {
            _renderTexture = new(width, height, 0)
            {
                enableRandomWrite = true
            };
            _renderTexture.Create();
        }


        _dataBuffer?.Release();
        _dataBuffer = new ComputeBuffer(_spriteDatas.Count, Marshal.SizeOf(typeof(TextureData)));
        _dataBuffer.SetData(_spriteDatas.ToArray());

        _computeShader.SetBuffer(kernel, "TextureDataBuffer", _dataBuffer);
        _computeShader.SetTexture(kernel, "Result", _renderTexture);

        foreach (var item in _spriteCollection)
        {
            if(item.sprite == null) continue;
            _computeShader.SetTexture(kernel, "TempSprite", item.sprite.texture);
            _computeShader.SetInt("DataIndex", _spriteCollection.IndexOf(item));
            int threadGroupsX = Mathf.CeilToInt(width / 8f);
            int threadGroupsY = Mathf.CeilToInt(height / 8f);
             _computeShader.Dispatch(kernel, threadGroupsX, threadGroupsY, 1);
        }
        ApplyToMaterial();
    }
    private void SetDatas()
    {
        _spriteDatas ??= new();
        _spriteCollection ??= new();

        if (_spriteDatas.Count < _spriteCollection.Count)
            _spriteDatas.Add(new TextureData());
        else if (_spriteDatas.Count > _spriteCollection.Count)
            _spriteDatas.RemoveAt(_spriteDatas.Count - 1);

        for (int i = 0; i < _spriteDatas.Count && i < _spriteCollection.Count; i++)
        {
            if(_spriteCollection[i].sprite == null) continue;
            Rect rect = _spriteCollection[i].sprite.textureRect;
            _spriteDatas[i] = new TextureData
            {
                canvasPos = _spriteCollection[i].canvasPos,
                color = _spriteCollection[i].color,
                spriteOffset = rect.position,
                canvasSize = rect.size
            };
        }
    }
    private void CaldulateTextureSize(out int width, out int height)
    {
        width = 0;
        height = 0;
        foreach (var data in _spriteDatas)
        {
            if (_spriteCollection[_spriteDatas.IndexOf(data)].sprite == null) continue;
            int right = Mathf.CeilToInt(data.canvasPos.x + data.canvasSize.x);
            int top = Mathf.CeilToInt(data.canvasPos.y + data.canvasSize.y);
            if (right > width) width = right;
            if (top > height) height = top;
        }
    }

    private void ApplyToMaterial()
    {
        if (_renderer == null) _renderer = GetComponent<Renderer>();
        _propBlock ??= new MaterialPropertyBlock();

        _renderer.GetPropertyBlock(_propBlock);
        _propBlock.SetTexture("_MainTex", _renderTexture); // Standart shader'lar
        _propBlock.SetTexture("_BaseMap", _renderTexture); // URP shader'ları
        _renderer.SetPropertyBlock(_propBlock);
    }

    private void Cleanup()
    {
        _dataBuffer?.Release();
        _dataBuffer = null;
        _renderTexture?.Release();
        _renderTexture = null;
    }
    #endregion

    #region Public funcs
    // Sprite funcs
    public void ChangeSprite(Sprite newSprite, string name)
    {
        SpriteData data = _spriteCollection.Find(s => s.spriteName == name);
        _spriteCollection[_spriteCollection.IndexOf(data)] = new SpriteData
        {
            spriteName = data.spriteName,
            sprite = newSprite,
            color = data.color,
            canvasPos = data.canvasPos
        };
    }
    public void ChangeSprite(Sprite newSprite, int index)
    {
        _spriteCollection[index] = new SpriteData
        {
            spriteName = _spriteCollection[index].spriteName,
            sprite = newSprite,
            color = _spriteCollection[index].color,
            canvasPos = _spriteCollection[index].canvasPos
        };
    }

    // Color funcs
    public void ChangeColor(Color newColor, string name)
    {
        SpriteData data = _spriteCollection.Find(s => s.spriteName == name);
        _spriteCollection[_spriteCollection.IndexOf(data)] = new SpriteData
        {
            spriteName = data.spriteName,
            sprite = data.sprite,
            color = newColor,
            canvasPos = data.canvasPos
        };
    }
    public void ChangeColor(Color newColor, int index)
    {
        _spriteCollection[index] = new SpriteData
        {
            spriteName = _spriteCollection[index].spriteName,
            sprite = _spriteCollection[index].sprite,
            color = newColor,
            canvasPos = _spriteCollection[index].canvasPos
        };
    }

    // Add & Remove
    public void AddSpriteData(SpriteData newData) => _spriteCollection.Add(newData);
    public void RemoveAtSpriteData(int index) => _spriteCollection.RemoveAt(index);
    #endregion
}