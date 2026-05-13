using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

[System.Serializable]
public struct TextureData
{
    public Vector2 canvasPos;
    [Header("Runtime Only")]
    public int layerIndex;
    public Vector2 spriteOffset; 
    public Vector2 canvasSize;
}

[ExecuteInEditMode]
public class SpriteBaker : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private List<Sprite> _spriteLibrary; 
    [SerializeField] private List<TextureData> _sprites;
    [SerializeField] private ComputeShader _computeShader;

    private RenderTexture _renderTexture;
    private ComputeBuffer _dataBuffer;
    private MaterialPropertyBlock _propBlock;
    private Renderer _renderer;

 
    private void Start() {
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
        _dataBuffer = new ComputeBuffer(_sprites.Count, Marshal.SizeOf(typeof(TextureData)));
        _dataBuffer.SetData(_sprites.ToArray());

        _computeShader.SetBuffer(kernel, "TextureDataBuffer", _dataBuffer);
        _computeShader.SetTexture(kernel, "Result", _renderTexture);

        foreach (var item in _spriteLibrary)
        {
            if(item == null) continue;
            _computeShader.SetTexture(kernel, "TempSprite", item.texture);
            _computeShader.SetInt("DataIndex", _spriteLibrary.IndexOf(item));
            int threadGroupsX = Mathf.CeilToInt(width / 8f);
            int threadGroupsY = Mathf.CeilToInt(height / 8f);
             _computeShader.Dispatch(kernel, threadGroupsX, threadGroupsY, 1);
        }
        ApplyToMaterial();
    }
    private void SetDatas()
    {
        _sprites ??= new();
        _spriteLibrary ??= new();

        if (_sprites.Count < _spriteLibrary.Count)
            _sprites.Add(new TextureData());
        else if (_sprites.Count > _spriteLibrary.Count)
            _sprites.RemoveAt(_sprites.Count - 1);

        for (int i = 0; i < _sprites.Count && i < _spriteLibrary.Count; i++)
        {
            if(_spriteLibrary[i] == null) continue;
            Rect rect = _spriteLibrary[i].textureRect;
            _sprites[i] = new TextureData
            {
                canvasPos = _sprites[i].canvasPos,
                layerIndex = i,
                spriteOffset = rect.position,
                canvasSize = rect.size
            };
        }
    }
    private void CaldulateTextureSize(out int width, out int height)
    {
        width = 0;
        height = 0;
        foreach (var data in _sprites)
        {
            if (_spriteLibrary[_sprites.IndexOf(data)] == null) continue;
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

}