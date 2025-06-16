using System;
using System.Collections.Generic;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.Common;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

class Desert : GameWindow
{
    private int _shaderProgram;
    private int _gradientShaderProgram;
    private int _modelLocation;
    private int _colorLocation;
    private int _colorChangeLocation;

    private int _pyramidVao;
    private int _pyramidVbo;
    private int _oasisVao;
    private int _oasisVbo;
    private int _oasisVertexCount;
    private int _starsVao;
    private int _starsVbo;
    private int _starCount = 100;
    private Random _rand = new Random();

    private float _time = 0f;
    private bool _timePaused = false;
    private float _fishAngle = 0f;
    private float _fishSpeed = 1.5f;
    private float _fishRadius = 0.12f;
    private bool _showGrass = true;

    private Vector4 _skyDayColor = new Vector4(0.529f, 0.808f, 0.922f, 1f);
    private Vector4 _skyNightColor = new Vector4(0.05f, 0.05f, 0.2f, 1f);
    private Vector4 _currentSkyColor;
    private Vector2 _sunPos;
    private Vector2 _moonPos;

    private float _colorChangeAmount = 0f;
    private const float COLOR_CHANGE_SPEED = 0.02f;

    private bool _showEntrances = false;
    private float _entranceProgress = 0f;
    private float _textAlpha = 0f;
    private float _textFadeProgress = 0f;
    private bool _showingText = false;
    private bool _fadingText = false;
    private float _closeTimer = 0f;
    private string _message = "Nastavice se na 3D projektu";
    private List<float> _letterAlphas = new List<float>();
    private int _textVao;
    private int _textVbo;

    public Desert() : base(GameWindowSettings.Default, new NativeWindowSettings()
    {
        Size = new Vector2i(1280, 720),
        Title = "Pustinja - Dan i Noc",
        Flags = ContextFlags.ForwardCompatible,
        APIVersion = new Version(3, 3),
    })
    {
        VSync = VSyncMode.On;
        for (int i = 0; i < _message.Length; i++)
        {
            _letterAlphas.Add(0f);
        }

        _textVao = GL.GenVertexArray();
        _textVbo = GL.GenBuffer();
    }

    protected override void OnLoad()
    {
        base.OnLoad();

        GL.ClearColor(_skyDayColor.X, _skyDayColor.Y, _skyDayColor.Z, _skyDayColor.W);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        _shaderProgram = CreateShaderProgram();
        _gradientShaderProgram = CreateGradientShaderProgram();
        _modelLocation = GL.GetUniformLocation(_shaderProgram, "model");
        _colorLocation = GL.GetUniformLocation(_shaderProgram, "inputColor");
        _colorChangeLocation = GL.GetUniformLocation(_gradientShaderProgram, "colorChangeAmount");

        InitPyramid();
        InitOasis();
        InitStars();
    }

    private int CreateShaderProgram()
    {
        string vertexShaderSource = @"#version 330 core
        layout(location = 0) in vec2 aPosition;
        uniform mat4 model;
        void main()
        {
            gl_Position = model * vec4(aPosition, 0.0, 1.0);
        }";

        string fragmentShaderSource = @"#version 330 core
        out vec4 FragColor;
        uniform vec4 inputColor;
        void main()
        {
            FragColor = inputColor;
        }";

        int vertexShader = GL.CreateShader(ShaderType.VertexShader);
        GL.ShaderSource(vertexShader, vertexShaderSource);
        GL.CompileShader(vertexShader);

        int fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
        GL.ShaderSource(fragmentShader, fragmentShaderSource);
        GL.CompileShader(fragmentShader);

        int program = GL.CreateProgram();
        GL.AttachShader(program, vertexShader);
        GL.AttachShader(program, fragmentShader);
        GL.LinkProgram(program);

        GL.DeleteShader(vertexShader);
        GL.DeleteShader(fragmentShader);

        return program;
    }

    private int CreateGradientShaderProgram()
    {
        string vertexShaderSource = @"#version 330 core
        layout(location = 0) in vec2 aPosition;
        uniform mat4 model;
        out float xPos;
        void main()
        {
            xPos = aPosition.x;
            gl_Position = model * vec4(aPosition, 0.0, 1.0);
        }";

        string fragmentShaderSource = @"#version 330 core
        in float xPos;
        out vec4 FragColor;
        uniform float colorChangeAmount;
        void main()
        {
            float blendFactor = clamp(colorChangeAmount - xPos, 0.0, 1.0);
            vec3 baseColor = vec3(0.803, 0.521, 0.247);
            vec3 redColor = vec3(0.8, 0.2, 0.2);
            FragColor = vec4(mix(baseColor, redColor, blendFactor), 1.0);
        }";

        int vertexShader = GL.CreateShader(ShaderType.VertexShader);
        GL.ShaderSource(vertexShader, vertexShaderSource);
        GL.CompileShader(vertexShader);

        int fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
        GL.ShaderSource(fragmentShader, fragmentShaderSource);
        GL.CompileShader(fragmentShader);

        int program = GL.CreateProgram();
        GL.AttachShader(program, vertexShader);
        GL.AttachShader(program, fragmentShader);
        GL.LinkProgram(program);

        GL.DeleteShader(vertexShader);
        GL.DeleteShader(fragmentShader);

        return program;
    }

    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        base.OnUpdateFrame(args);

        if (KeyboardState.IsKeyDown(Keys.Escape))
            Close();

        if (KeyboardState.IsKeyPressed(Keys.D1))
            _showGrass = false;
        else if (KeyboardState.IsKeyPressed(Keys.D2))
            _showGrass = true;

        if (KeyboardState.IsKeyPressed(Keys.P))
            _timePaused = !_timePaused;

        if (KeyboardState.IsKeyPressed(Keys.R))
        {
            _time = 0.2f;
            _timePaused = false;
        }

        if (KeyboardState.IsKeyPressed(Keys.O))
        {
            _showEntrances = !_showEntrances;
            if (_showEntrances)
            {
                _entranceProgress = 0f;
                _showingText = false;
                _fadingText = false;
                _textAlpha = 0f;
                _textFadeProgress = 0f;
                _closeTimer = 0f;
                for (int i = 0; i < _letterAlphas.Count; i++)
                    _letterAlphas[i] = 0f;
            }
        }

        if (!_timePaused)
            _time += (float)args.Time * 0.1f;

        _fishAngle += (float)args.Time * _fishSpeed;

        if (KeyboardState.IsKeyDown(Keys.A))
            _colorChangeAmount = MathHelper.Clamp(_colorChangeAmount - COLOR_CHANGE_SPEED, 0f, 1f);
        else if (KeyboardState.IsKeyDown(Keys.D))
            _colorChangeAmount = MathHelper.Clamp(_colorChangeAmount + COLOR_CHANGE_SPEED, 0f, 1f);

        if (_showEntrances && _entranceProgress < 1f)
        {
            _entranceProgress += (float)args.Time * 0.5f;
            if (_entranceProgress >= 1f)
            {
                _entranceProgress = 1f;
                _showingText = true;
            }
        }

        if (_showingText)
        {
            _textAlpha = MathHelper.Clamp(_textAlpha + (float)args.Time * 0.5f, 0f, 1f);

            for (int i = 0; i < _letterAlphas.Count; i++)
            {
                float targetAlpha = (_textFadeProgress * _message.Length > i) ? 1f : 0f;
                _letterAlphas[i] = MathHelper.Clamp(_letterAlphas[i] + (float)args.Time * 2f * (targetAlpha - _letterAlphas[i]), 0f, 1f);
            }

            _textFadeProgress += (float)args.Time * 0.3f;

            if (_textFadeProgress >= 1f && !_fadingText)
            {
                _fadingText = true;
                _textFadeProgress = 0f;
            }

            if (_fadingText)
            {
                _textAlpha = MathHelper.Clamp(_textAlpha - (float)args.Time * 0.5f, 0f, 1f);
                if (_textAlpha <= 0f)
                {
                    _closeTimer += (float)args.Time;
                    if (_closeTimer >= 2f)
                        Close();
                }
            }
        }

        float fullAngle = (_time % 1f) * 2 * MathF.PI;
        _sunPos = new Vector2(MathF.Cos(fullAngle - MathF.PI / 2), MathF.Sin(fullAngle - MathF.PI / 2));
        _moonPos = new Vector2(MathF.Cos(fullAngle + MathF.PI / 2), MathF.Sin(fullAngle + MathF.PI / 2));

        float t = MathHelper.Clamp((_sunPos.Y + 1) / 2, 0, 1);
        _currentSkyColor = Vector4.Lerp(_skyNightColor, _skyDayColor, t);
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);

        GL.ClearColor(_currentSkyColor.X, _currentSkyColor.Y, _currentSkyColor.Z, _currentSkyColor.W);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        if (_sunPos.Y > 0)
            DrawCircle(_sunPos, 0.1f, new Vector4(1f, 1f, 0f, 1f));

        if (_sunPos.Y <= 0)
        {
            DrawCircle(_moonPos, 0.07f, new Vector4(0.8f, 0.8f, 0.8f, 1f));
            DrawStars();
        }

        if (_showEntrances)
        {
            DrawPyramidWithEntrance(new Vector2(-0.8f, -0.5f), 0.4f, new Vector4(0.956f, 0.643f, 0.376f, 1.0f));
            DrawPyramidWithEntrance(new Vector2(-0.2f, -0.5f), 0.6f, new Vector4(0.803f, 0.521f, 0.247f, 1.0f));
            DrawPyramidWithEntrance(new Vector2(0.5f, -0.5f), 0.4f, new Vector4(0.956f, 0.643f, 0.376f, 1.0f));
        }
        else
        {
            DrawPyramid(new Vector2(-0.8f, -0.5f), 0.4f, new Vector4(0.956f, 0.643f, 0.376f, 1.0f));
            DrawLeftToRightGradientPyramid(new Vector2(-0.2f, -0.5f), 0.6f);
            DrawPyramid(new Vector2(0.5f, -0.5f), 0.4f, new Vector4(0.956f, 0.643f, 0.376f, 1.0f));
        }

        DrawOasis(new Vector2(0.6f, -0.6f), 0.2f,
            new Vector4(0.392f, 0.584f, 0.929f, 1.0f),
            new Vector4(0.133f, 0.545f, 0.133f, 1.0f));

        DrawFish(new Vector2(0.6f, -0.6f), _fishRadius, _fishAngle, new Vector4(1f, 0f, 0f, 1f));

        if (_showingText)
            DrawTextMessage();

        DrawSignature();
        SwapBuffers();
        
    }

    private void DrawSignature()
    {
        string signature = "Mirko Mandic RA 174-2015";
        float scale = 0.08f;
        float spacing = scale * 1.1f;
        float x = -0.95f;
        float y = -0.85f;

        GL.UseProgram(_shaderProgram);

        for (int i = 0; i < signature.Length; i++)
        {
            char c = signature[i];
            Vector4 letterColor = new Vector4(1f, 1f, 1f, 1f);
            DrawLetter(c, new Vector2(x, y), scale, letterColor);
            x += spacing;
        }
    }



    private void DrawLeftToRightGradientPyramid(Vector2 position, float size)
    {
        GL.UseProgram(_gradientShaderProgram);
        GL.Uniform1(_colorChangeLocation, _colorChangeAmount);

        Matrix4 model = Matrix4.CreateScale(size, size, 1f) *
                       Matrix4.CreateTranslation(position.X, position.Y, 0);
        GL.UniformMatrix4(_modelLocation, false, ref model);

        GL.BindVertexArray(_pyramidVao);
        GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
        GL.BindVertexArray(0);
    }

    private void InitPyramid()
    {
        float[] vertices = { 0f, 0f, 0.5f, 1f, 1f, 0f };

        _pyramidVao = GL.GenVertexArray();
        _pyramidVbo = GL.GenBuffer();

        GL.BindVertexArray(_pyramidVao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _pyramidVbo);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);
        GL.BindVertexArray(0);
    }

    private void DrawPyramid(Vector2 position, float size, Vector4 color)
    {
        Matrix4 model = Matrix4.CreateScale(size, size, 1f) *
                        Matrix4.CreateTranslation(position.X, position.Y, 0);

        GL.UseProgram(_shaderProgram);
        GL.UniformMatrix4(_modelLocation, false, ref model);
        GL.Uniform4(_colorLocation, color);
        GL.BindVertexArray(_pyramidVao);
        GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
        GL.BindVertexArray(0);
    }

    private void DrawPyramidWithEntrance(Vector2 position, float size, Vector4 color)
    {
        float entranceWidth = 0.2f * _entranceProgress;
        float entranceHeight = 0.5f * _entranceProgress; // Half the pyramid height

        // Create pyramid with rectangular entrance
        List<float> vertices = new List<float>();

        // Main pyramid outline minus the entrance
        vertices.AddRange(new float[] { 0f, 0f }); // Bottom left
        vertices.AddRange(new float[] { 0.5f - entranceWidth / 2, 0f }); // Left edge of entrance
        vertices.AddRange(new float[] { 0.5f, entranceHeight }); // Top of entrance left

        vertices.AddRange(new float[] { 0.5f, entranceHeight }); // Top of entrance left
        vertices.AddRange(new float[] { 0.5f, 1f }); // Top of pyramid
        vertices.AddRange(new float[] { 0f, 0f }); // Bottom left

        vertices.AddRange(new float[] { 0.5f + entranceWidth / 2, 0f }); // Right edge of entrance
        vertices.AddRange(new float[] { 1f, 0f }); // Bottom right
        vertices.AddRange(new float[] { 0.5f, entranceHeight }); // Top of entrance right

        vertices.AddRange(new float[] { 0.5f, entranceHeight }); // Top of entrance right
        vertices.AddRange(new float[] { 1f, 0f }); // Bottom right
        vertices.AddRange(new float[] { 0.5f, 1f }); // Top of pyramid

        int vao = GL.GenVertexArray();
        int vbo = GL.GenBuffer();

        GL.BindVertexArray(vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Count * sizeof(float), vertices.ToArray(), BufferUsageHint.StaticDraw);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);

        Matrix4 model = Matrix4.CreateScale(size, size, 1f) *
                       Matrix4.CreateTranslation(position.X, position.Y, 0);

        GL.UseProgram(_shaderProgram);
        GL.UniformMatrix4(_modelLocation, false, ref model);
        GL.Uniform4(_colorLocation, color);
        GL.DrawArrays(PrimitiveType.Triangles, 0, vertices.Count / 2);

        // Draw the entrance as a black rectangle
        if (_entranceProgress > 0)
        {
            float[] entranceVertices = {
                0.5f - entranceWidth/2, 0f,
                0.5f + entranceWidth/2, 0f,
                0.5f + entranceWidth/2, entranceHeight,
                0.5f - entranceWidth/2, entranceHeight
            };  

            int entranceVao = GL.GenVertexArray();
            int entranceVbo = GL.GenBuffer();

            GL.BindVertexArray(entranceVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, entranceVbo);
            GL.BufferData(BufferTarget.ArrayBuffer, entranceVertices.Length * sizeof(float), entranceVertices, BufferUsageHint.StaticDraw);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);

            GL.UniformMatrix4(_modelLocation, false, ref model);
            GL.Uniform4(_colorLocation, new Vector4(0f, 0f, 0f, 1f)); // Black entrance
            GL.DrawArrays(PrimitiveType.TriangleFan, 0, 4);

            GL.BindVertexArray(0);
            GL.DeleteBuffer(entranceVbo);
            GL.DeleteVertexArray(entranceVao);
        }

        GL.BindVertexArray(0);
        GL.DeleteBuffer(vbo);
        GL.DeleteVertexArray(vao);
    }

    private void DrawTextMessage()
    {
        GL.UseProgram(_shaderProgram);

        float scale = 0.03f;
        float x = -0.45f;
        float y = 0.7f;
        float spacing = 0.03f;

        for (int i = 0; i < _message.Length; i++)
        {
            char c = _message[i];
            if (_letterAlphas[i] > 0.01f)
            {
                Vector4 letterColor = new Vector4(1f, 1f, 1f, _textAlpha * _letterAlphas[i]);
                DrawLetter(c, new Vector2(x, y), scale, letterColor);
            }
            x += spacing;
        }
    }

    private void DrawLetter(char c, Vector2 position, float size, Vector4 color)
    {
        List<float[]> letterSegments = GetLetterSegments(c);

        GL.UseProgram(_shaderProgram);
        GL.Uniform4(_colorLocation, color);

        foreach (var segment in letterSegments)
        {
            int vao = GL.GenVertexArray();
            int vbo = GL.GenBuffer();

            GL.BindVertexArray(vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, segment.Length * sizeof(float), segment, BufferUsageHint.StaticDraw);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);

            Matrix4 model = Matrix4.CreateScale(size, size, 1f) *
                           Matrix4.CreateTranslation(position.X, position.Y, 0);

            GL.UniformMatrix4(_modelLocation, false, ref model);
            GL.DrawArrays(PrimitiveType.LineStrip, 0, segment.Length / 2);

            GL.BindVertexArray(0);
            GL.DeleteBuffer(vbo);
            GL.DeleteVertexArray(vao);
        }
    }

    private List<float[]> GetLetterSegments(char c)
    {
        List<float[]> segments = new List<float[]>();

        switch (char.ToUpper(c))
        {
            case 'N':
                segments.Add(new float[] { -0.5f, -0.5f, -0.5f, 0.5f });
                segments.Add(new float[] { -0.5f, 0.5f, 0.5f, -0.5f });
                segments.Add(new float[] { 0.5f, -0.5f, 0.5f, 0.5f });
                break;
            case 'A':
                segments.Add(new float[] { -0.5f, -0.5f, 0f, 0.5f });
                segments.Add(new float[] { 0f, 0.5f, 0.5f, -0.5f });
                segments.Add(new float[] { -0.25f, 0f, 0.25f, 0f });
                break;
            case 'S':
                segments.Add(new float[] { 0.5f, -0.5f, -0.5f, -0.5f, -0.5f, 0f, 0.5f, 0f, 0.5f, 0.5f, -0.5f, 0.5f });
                break;
            case 'T':
                segments.Add(new float[] { -0.5f, 0.5f, 0.5f, 0.5f });
                segments.Add(new float[] { 0f, 0.5f, 0f, -0.5f });
                break;
            case 'V':
                segments.Add(new float[] { -0.5f, 0.5f, 0f, -0.5f });
                segments.Add(new float[] { 0f, -0.5f, 0.5f, 0.5f });
                break;
            case 'I':
                segments.Add(new float[] { -0.3f, 0.5f, 0.3f, 0.5f });
                segments.Add(new float[] { 0f, 0.5f, 0f, -0.5f });
                segments.Add(new float[] { -0.3f, -0.5f, 0.3f, -0.5f });
                break;
            case 'C':
                segments.Add(new float[] { 0.5f, 0.5f, -0.5f, 0.5f, -0.5f, -0.5f, 0.5f, -0.5f });
                break;
            case 'E':
                segments.Add(new float[] { -0.5f, -0.5f, -0.5f, 0.5f });
                segments.Add(new float[] { -0.5f, 0.5f, 0.5f, 0.5f });
                segments.Add(new float[] { -0.5f, 0f, 0.3f, 0f });
                segments.Add(new float[] { -0.5f, -0.5f, 0.5f, -0.5f });
                break;
            case 'U':
                segments.Add(new float[] { -0.5f, 0.5f, -0.5f, -0.5f, 0.5f, -0.5f, 0.5f, 0.5f });
                break;
            case 'D':
                segments.Add(new float[] { -0.5f, -0.5f, -0.5f, 0.5f, 0.3f, 0.5f, 0.5f, 0.3f, 0.5f, -0.3f, 0.3f, -0.5f, -0.5f, -0.5f });
                break;
            case 'R':
                segments.Add(new float[] { -0.5f, -0.5f, -0.5f, 0.5f });
                segments.Add(new float[] { -0.5f, 0.5f, 0.3f, 0.5f, 0.5f, 0.3f, 0.5f, 0f, -0.5f, 0f });
                segments.Add(new float[] { 0f, 0f, 0.5f, -0.5f });
                break;
            case 'O':
                segments.Add(new float[] { -0.5f, -0.5f, -0.5f, 0.5f, 0.5f, 0.5f, 0.5f, -0.5f, -0.5f, -0.5f });
                break;
            case 'J':
                segments.Add(new float[] { -0.5f, 0.5f, 0.5f, 0.5f });
                segments.Add(new float[] { 0f, 0.5f, 0f, -0.3f, -0.3f, -0.5f, -0.5f, -0.3f });
                break;
            case 'K':
                segments.Add(new float[] { -0.5f, -0.5f, -0.5f, 0.5f });
                segments.Add(new float[] { -0.5f, 0f, 0.5f, 0.5f });
                segments.Add(new float[] { -0.5f, 0f, 0.5f, -0.5f });
                break;
            case 'P':
                segments.Add(new float[] { -0.5f, -0.5f, -0.5f, 0.5f, 0.3f, 0.5f, 0.5f, 0.3f, 0.3f, 0f, -0.5f, 0f });
                break;
            case 'L':
                segments.Add(new float[] { -0.5f, 0.5f, -0.5f, -0.5f, 0.5f, -0.5f });
                break;
            case 'M':
                segments.Add(new float[] { -0.5f, -0.5f, -0.5f, 0.5f });
                segments.Add(new float[] { -0.5f, 0.5f, 0f, 0f });
                segments.Add(new float[] { 0f, 0f, 0.5f, 0.5f });
                segments.Add(new float[] { 0.5f, 0.5f, 0.5f, -0.5f });
                break;
            case '3':
                segments.Add(new float[] { -0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0f, 0f, 0f });
                segments.Add(new float[] { 0f, 0f, 0.5f, 0f, 0.5f, -0.5f, -0.5f, -0.5f });
                break;
            case ' ':
                // Space - no segments
                break;
            default:
                // Default to drawing a rectangle for unknown characters
                segments.Add(new float[] { -0.5f, -0.5f, 0.5f, -0.5f, 0.5f, 0.5f, -0.5f, 0.5f, -0.5f, -0.5f });
                break;
        }

        return segments;
    }

    private void InitOasis()
    {
        int segments = 100;
        _oasisVertexCount = segments + 2;
        float[] vertices = new float[_oasisVertexCount * 2];

        vertices[0] = 0f;
        vertices[1] = 0f;

        for (int i = 0; i <= segments; i++)
        {
            double angle = i * 2.0 * Math.PI / segments;
            vertices[(i + 1) * 2] = (float)Math.Cos(angle);
            vertices[(i + 1) * 2 + 1] = (float)Math.Sin(angle);
        }

        _oasisVao = GL.GenVertexArray();
        _oasisVbo = GL.GenBuffer();

        GL.BindVertexArray(_oasisVao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _oasisVbo);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);
        GL.BindVertexArray(0);
    }

    private void DrawOasis(Vector2 position, float radius, Vector4 waterColor, Vector4 grassColor)
    {
        Matrix4 model = Matrix4.CreateScale(radius, radius, 1f) *
                        Matrix4.CreateTranslation(position.X, position.Y, 0);
        GL.UseProgram(_shaderProgram);
        GL.UniformMatrix4(_modelLocation, false, ref model);
        GL.Uniform4(_colorLocation, waterColor);
        GL.BindVertexArray(_oasisVao);
        GL.DrawArrays(PrimitiveType.TriangleFan, 0, _oasisVertexCount);
        GL.BindVertexArray(0);

        if (_showGrass)
        {
            GL.Uniform4(_colorLocation, grassColor);
            float bladeWidth = radius / 10f;

            for (int i = 0; i < 10; i++)
            {
                float angle = i * 2 * MathF.PI / 10;
                float tx = position.X + MathF.Cos(angle) * radius;
                float ty = position.Y + MathF.Sin(angle) * radius;

                Matrix4 grassModel = Matrix4.CreateScale(bladeWidth, 0.05f, 1f) *
                                    Matrix4.CreateTranslation(tx, ty + 0.025f, 0);
                GL.UniformMatrix4(_modelLocation, false, ref grassModel);

                float[] quadVertices = { -0.5f, 0f, 0.5f, 0f, 0.5f, 1f, -0.5f, 1f };

                int grassVao = GL.GenVertexArray();
                int grassVbo = GL.GenBuffer();

                GL.BindVertexArray(grassVao);
                GL.BindBuffer(BufferTarget.ArrayBuffer, grassVbo);
                GL.BufferData(BufferTarget.ArrayBuffer, quadVertices.Length * sizeof(float), quadVertices, BufferUsageHint.StaticDraw);
                GL.EnableVertexAttribArray(0);
                GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);
                GL.DrawArrays(PrimitiveType.TriangleFan, 0, 4);

                GL.BindVertexArray(0);
                GL.DeleteBuffer(grassVbo);
                GL.DeleteVertexArray(grassVao);
            }
        }
    }

    private void InitStars()
    {
        float[] starPositions = new float[_starCount * 2];
        for (int i = 0; i < _starCount; i++)
        {
            starPositions[i * 2] = (float)(_rand.NextDouble() * 2.0 - 1.0);
            starPositions[i * 2 + 1] = (float)(_rand.NextDouble() * 0.8 + 0.2);
        }

        _starsVao = GL.GenVertexArray();
        _starsVbo = GL.GenBuffer();

        GL.BindVertexArray(_starsVao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _starsVbo);
        GL.BufferData(BufferTarget.ArrayBuffer, starPositions.Length * sizeof(float), starPositions, BufferUsageHint.StaticDraw);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);
        GL.BindVertexArray(0);
    }

    private void DrawStars()
    {
        Matrix4 model = Matrix4.Identity;
        GL.UseProgram(_shaderProgram);
        GL.UniformMatrix4(_modelLocation, false, ref model);
        GL.Uniform4(_colorLocation, new Vector4(1f, 1f, 1f, 1f));
        GL.BindVertexArray(_starsVao);
        GL.PointSize(2f);
        GL.DrawArrays(PrimitiveType.Points, 0, _starCount);
        GL.BindVertexArray(0);
    }

    private void DrawCircle(Vector2 center, float radius, Vector4 color)
    {
        int segments = 50;
        float[] vertices = new float[(segments + 2) * 2];
        vertices[0] = 0f;
        vertices[1] = 0f;

        for (int i = 0; i <= segments; i++)
        {
            float angle = i * 2.0f * MathF.PI / segments;
            vertices[(i + 1) * 2] = MathF.Cos(angle);
            vertices[(i + 1) * 2 + 1] = MathF.Sin(angle);
        }

        int vao = GL.GenVertexArray();
        int vbo = GL.GenBuffer();

        GL.BindVertexArray(vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);

        Matrix4 model = Matrix4.CreateScale(radius, radius, 1f) *
                        Matrix4.CreateTranslation(center.X, center.Y, 0f);
        GL.UseProgram(_shaderProgram);
        GL.UniformMatrix4(_modelLocation, false, ref model);
        GL.Uniform4(_colorLocation, color);
        GL.DrawArrays(PrimitiveType.TriangleFan, 0, segments + 2);

        GL.BindVertexArray(0);
        GL.DeleteBuffer(vbo);
        GL.DeleteVertexArray(vao);
    }

    private void DrawFish(Vector2 center, float radius, float angle, Vector4 color)
    {
        Vector2 fishPos = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;

        float[] bodyVertices = { 0f, 0.04f, -0.03f, -0.02f, 0.03f, -0.02f };
        float[] tailVertices = { -0.03f, 0.015f, -0.06f, -0.02f, -0.03f, -0.05f };

        int bodyVao = GL.GenVertexArray();
        int bodyVbo = GL.GenBuffer();

        GL.BindVertexArray(bodyVao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, bodyVbo);
        GL.BufferData(BufferTarget.ArrayBuffer, bodyVertices.Length * sizeof(float), bodyVertices, BufferUsageHint.StaticDraw);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);

        Matrix4 modelBody = Matrix4.CreateRotationZ(angle + MathF.PI / 2) *
                           Matrix4.CreateTranslation(fishPos.X, fishPos.Y, 0f);

        GL.UseProgram(_shaderProgram);
        GL.UniformMatrix4(_modelLocation, false, ref modelBody);
        GL.Uniform4(_colorLocation, color);
        GL.DrawArrays(PrimitiveType.Triangles, 0, 3);

        GL.BindVertexArray(0);
        GL.DeleteBuffer(bodyVbo);
        GL.DeleteVertexArray(bodyVao);

        int tailVao = GL.GenVertexArray();
        int tailVbo = GL.GenBuffer();

        GL.BindVertexArray(tailVao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, tailVbo);
        GL.BufferData(BufferTarget.ArrayBuffer, tailVertices.Length * sizeof(float), tailVertices, BufferUsageHint.StaticDraw);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);

        GL.UniformMatrix4(_modelLocation, false, ref modelBody);
        GL.Uniform4(_colorLocation, color);
        GL.DrawArrays(PrimitiveType.Triangles, 0, 3);

        GL.BindVertexArray(0);
        GL.DeleteBuffer(tailVbo);
        GL.DeleteVertexArray(tailVao);
    }
}