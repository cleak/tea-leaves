using Godot;
using System;
using System.Collections.Generic;

namespace TeaLeaves
{
    /// <summary>
    /// PIKUI: A neon slide-painting puzzle game.
    /// Guide a glowing orb across a dark grid, painting tiles with WASD directional slides.
    /// Each direction paints a different neon color. Fill the grid to complete each level.
    /// </summary>
    public partial class PikuiGame : Node3D
    {
        private const float TileSpacing = 1.08f;
        private const float TileThickness = 0.06f;
        private const float TileWidth = 0.96f;
        private const float OrbRadius = 0.2f;
        private const float OrbY = 0.35f;
        private const float SlideTimePerTile = 0.055f;
        private const float BorderThickness = 0.08f;
        private const float BorderHeight = 0.12f;

        private PikuiLogic _logic = null!;
        private int _currentLevel = 1;
        private int _totalScore;
        private Phase _phase = Phase.Idle;

        private enum Phase { Idle, Sliding, Complete, AllDone }

        // Slide animation
        private List<(int x, int y, bool wasNew)> _slidePath = new();
        private int _slideIndex;
        private float _slideElapsed;
        private float _slideDuration;
        private Vector3 _slideFrom;
        private Vector3 _slideTo;
        private SlideDirection _slideDir;

        // Nodes
        private MeshInstance3D[,] _tileMeshes = null!;
        private MeshInstance3D _orbMesh = null!;
        private OmniLight3D _orbLight = null!;
        private Node3D _levelRoot = null!;
        private Camera3D _camera = null!;

        // UI
        private CanvasLayer _ui = null!;
        private Label _titleLabel = null!;
        private Label _scoreLabel = null!;
        private Label _comboLabel = null!;
        private Label _movesLabel = null!;
        private Label _levelLabel = null!;
        private ProgressBar _progressBar = null!;
        private Label _progressText = null!;
        private Label _messageLabel = null!;
        private Label[] _arrows = new Label[4];
        private float[] _arrowFlash = new float[4];
        private Label _parLabel = null!;

        // Ambient
        private AudioStreamPlayer _ambientPlayer = null!;
        private float _ambientProgress;

        public override void _Ready()
        {
            BuildCamera();
            BuildEnvironment();
            BuildUI();
            _ambientPlayer = new AudioStreamPlayer();
            AddChild(_ambientPlayer);
            _ambientPlayer.VolumeDb = -6;
            StartLevel(_currentLevel);
        }

        private void StartLevel(int level)
        {
            _currentLevel = level;
            _logic = new PikuiLogic(level);
            _phase = Phase.Idle;

            if (_levelRoot != null)
            {
                RemoveChild(_levelRoot);
                _levelRoot.QueueFree();
            }
            _levelRoot = new Node3D { Name = "LevelRoot" };
            AddChild(_levelRoot);

            BuildGrid();
            BuildOrb();
            BuildBorder();
            PaintTileVisual(_logic.PlayerPos.x, _logic.PlayerPos.y, PaintColor.Cyan);
            UpdateUI();
            UpdateAmbient();

            _messageLabel.Text = $"LEVEL {_currentLevel}";
            _messageLabel.Visible = true;

            var timer = GetTree().CreateTimer(1.5);
            timer.Timeout += () => { if (_phase == Phase.Idle) _messageLabel.Visible = false; };
        }

        private void BuildCamera()
        {
            _camera = new Camera3D();
            AddChild(_camera);
            _camera.Position = new Vector3(0, 16, 7);
            _camera.LookAt(new Vector3(0, 0, -0.5f));
            _camera.Fov = 50;
            _camera.Current = true;
        }

        private void BuildEnvironment()
        {
            var env = new Godot.Environment();
            env.BackgroundMode = Godot.Environment.BGMode.Color;
            env.BackgroundColor = new Color(0.01f, 0.01f, 0.04f);
            env.AmbientLightSource = Godot.Environment.AmbientSource.Color;
            env.AmbientLightColor = new Color(0.15f, 0.15f, 0.25f);
            env.AmbientLightEnergy = 0.6f;
            env.GlowEnabled = true;
            env.GlowIntensity = 0.6f;
            env.GlowStrength = 0.7f;
            env.GlowBloom = 0.05f;
            env.GlowHdrThreshold = 1.0f;
            env.TonemapMode = Godot.Environment.ToneMapper.Aces;

            var we = new WorldEnvironment();
            we.Environment = env;
            AddChild(we);

            var light = new DirectionalLight3D();
            light.LightColor = new Color(0.6f, 0.6f, 0.8f);
            light.LightEnergy = 0.3f;
            light.RotationDegrees = new Vector3(-60, 30, 0);
            AddChild(light);
        }

        private void BuildGrid()
        {
            int size = _logic.GridSize;
            _tileMeshes = new MeshInstance3D[size, size];
            var tileMesh = new BoxMesh();
            tileMesh.Size = new Vector3(TileWidth, TileThickness, TileWidth);

            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    if (_logic.IsWall(x, y))
                    {
                        var wall = new MeshInstance3D();
                        var wallMesh = new BoxMesh();
                        wallMesh.Size = new Vector3(TileWidth, 0.3f, TileWidth);
                        wall.Mesh = wallMesh;
                        wall.Position = GridToWorld(x, y) + new Vector3(0, 0.15f, 0);
                        var wm = new StandardMaterial3D();
                        wm.AlbedoColor = new Color(0.06f, 0.06f, 0.08f);
                        wm.Metallic = 0.4f;
                        wm.Roughness = 0.6f;
                        wm.EmissionEnabled = true;
                        wm.Emission = new Color(0.1f, 0.05f, 0.15f);
                        wm.EmissionEnergyMultiplier = 0.3f;
                        wall.MaterialOverride = wm;
                        _levelRoot.AddChild(wall);
                        continue;
                    }

                    var tile = new MeshInstance3D();
                    tile.Mesh = tileMesh;
                    tile.Position = GridToWorld(x, y);
                    var mat = new StandardMaterial3D();
                    mat.AlbedoColor = new Color(0.12f, 0.12f, 0.16f);
                    mat.Metallic = 0.1f;
                    mat.Roughness = 0.85f;
                    mat.EmissionEnabled = true;
                    mat.Emission = new Color(0.08f, 0.08f, 0.12f);
                    mat.EmissionEnergyMultiplier = 0.2f;
                    tile.MaterialOverride = mat;
                    _levelRoot.AddChild(tile);
                    _tileMeshes[x, y] = tile;
                }
            }
        }

        private void BuildOrb()
        {
            _orbMesh = new MeshInstance3D();
            var sphere = new SphereMesh();
            sphere.Radius = OrbRadius;
            sphere.Height = OrbRadius * 2;
            sphere.RadialSegments = 24;
            sphere.Rings = 12;
            _orbMesh.Mesh = sphere;

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(0.9f, 0.95f, 1.0f);
            mat.EmissionEnabled = true;
            mat.Emission = new Color(0.8f, 0.9f, 1.0f);
            mat.EmissionEnergyMultiplier = 2.5f;
            mat.Metallic = 0.3f;
            mat.Roughness = 0.2f;
            _orbMesh.MaterialOverride = mat;

            var (px, py) = _logic.PlayerPos;
            _orbMesh.Position = GridToWorld(px, py) + new Vector3(0, OrbY, 0);
            _levelRoot.AddChild(_orbMesh);

            _orbLight = new OmniLight3D();
            _orbLight.LightColor = new Color(0.7f, 0.85f, 1.0f);
            _orbLight.LightEnergy = 1.8f;
            _orbLight.OmniRange = 4.0f;
            _orbLight.OmniAttenuation = 1.5f;
            _orbMesh.AddChild(_orbLight);
        }

        private void BuildBorder()
        {
            int size = _logic.GridSize;
            float half = (size - 1) * TileSpacing / 2.0f;
            float edge = half + TileWidth / 2 + BorderThickness / 2 + 0.04f;
            float len = size * TileSpacing + BorderThickness + 0.08f;

            var borderMat = new StandardMaterial3D();
            borderMat.AlbedoColor = new Color(0.15f, 0.1f, 0.25f);
            borderMat.EmissionEnabled = true;
            borderMat.Emission = new Color(0.3f, 0.15f, 0.5f);
            borderMat.EmissionEnergyMultiplier = 0.8f;
            borderMat.Metallic = 0.5f;
            borderMat.Roughness = 0.4f;

            void AddBorderSegment(Vector3 pos, Vector3 meshSize)
            {
                var seg = new MeshInstance3D();
                var box = new BoxMesh();
                box.Size = meshSize;
                seg.Mesh = box;
                seg.Position = pos;
                seg.MaterialOverride = borderMat;
                _levelRoot.AddChild(seg);
            }

            AddBorderSegment(new Vector3(0, BorderHeight / 2, -edge), new Vector3(len, BorderHeight, BorderThickness));
            AddBorderSegment(new Vector3(0, BorderHeight / 2, edge), new Vector3(len, BorderHeight, BorderThickness));
            AddBorderSegment(new Vector3(-edge, BorderHeight / 2, 0), new Vector3(BorderThickness, BorderHeight, len));
            AddBorderSegment(new Vector3(edge, BorderHeight / 2, 0), new Vector3(BorderThickness, BorderHeight, len));
        }

        private void BuildUI()
        {
            _ui = new CanvasLayer();
            AddChild(_ui);

            _titleLabel = MakeLabel("PIKUI", 52, new Color(0.7f, 0.85f, 1.0f));
            _titleLabel.Position = new Vector2(960, 16);
            _titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _titleLabel.Size = new Vector2(0, 0);
            _titleLabel.GrowHorizontal = Control.GrowDirection.Both;

            _levelLabel = MakeLabel("LEVEL 1", 22, new Color(0.6f, 0.5f, 0.8f));
            _levelLabel.Position = new Vector2(40, 28);

            _scoreLabel = MakeLabel("SCORE: 0", 28, new Color(1, 1, 1));
            _scoreLabel.Position = new Vector2(1560, 24);
            _scoreLabel.HorizontalAlignment = HorizontalAlignment.Right;
            _scoreLabel.Size = new Vector2(320, 40);

            _comboLabel = MakeLabel("", 24, new Color(1, 0.9f, 0.3f));
            _comboLabel.Position = new Vector2(1560, 62);
            _comboLabel.HorizontalAlignment = HorizontalAlignment.Right;
            _comboLabel.Size = new Vector2(320, 36);

            _movesLabel = MakeLabel("MOVES: 0", 20, new Color(0.7f, 0.7f, 0.7f));
            _movesLabel.Position = new Vector2(1640, 100);
            _movesLabel.HorizontalAlignment = HorizontalAlignment.Right;
            _movesLabel.Size = new Vector2(240, 30);

            _parLabel = MakeLabel("PAR: 14", 20, new Color(0.5f, 0.5f, 0.6f));
            _parLabel.Position = new Vector2(1640, 126);
            _parLabel.HorizontalAlignment = HorizontalAlignment.Right;
            _parLabel.Size = new Vector2(240, 30);

            _progressBar = new ProgressBar();
            _progressBar.Position = new Vector2(460, 1030);
            _progressBar.Size = new Vector2(1000, 28);
            _progressBar.MinValue = 0;
            _progressBar.MaxValue = 100;
            _progressBar.Value = 0;
            _progressBar.ShowPercentage = false;

            var pbStyle = new StyleBoxFlat();
            pbStyle.BgColor = new Color(0.1f, 0.1f, 0.15f);
            pbStyle.CornerRadiusBottomLeft = 4;
            pbStyle.CornerRadiusBottomRight = 4;
            pbStyle.CornerRadiusTopLeft = 4;
            pbStyle.CornerRadiusTopRight = 4;
            _progressBar.AddThemeStyleboxOverride("background", pbStyle);

            var fillStyle = new StyleBoxFlat();
            fillStyle.BgColor = new Color(0.3f, 0.8f, 1.0f);
            fillStyle.CornerRadiusBottomLeft = 4;
            fillStyle.CornerRadiusBottomRight = 4;
            fillStyle.CornerRadiusTopLeft = 4;
            fillStyle.CornerRadiusTopRight = 4;
            _progressBar.AddThemeStyleboxOverride("fill", fillStyle);
            _ui.AddChild(_progressBar);

            _progressText = MakeLabel("0%", 18, new Color(0.9f, 0.9f, 0.9f));
            _progressText.Position = new Vector2(960, 1032);
            _progressText.HorizontalAlignment = HorizontalAlignment.Center;
            _progressText.Size = new Vector2(0, 0);
            _progressText.GrowHorizontal = Control.GrowDirection.Both;

            _messageLabel = MakeLabel("", 56, new Color(1, 0.95f, 0.6f));
            _messageLabel.Position = new Vector2(960, 460);
            _messageLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _messageLabel.Size = new Vector2(0, 0);
            _messageLabel.GrowHorizontal = Control.GrowDirection.Both;
            _messageLabel.Visible = false;

            BuildControlsDisplay();
        }

        private void BuildControlsDisplay()
        {
            float bx = 160, by = 920;
            string[] symbols = { "\u25b2", "\u25b6", "\u25bc", "\u25c0" };
            string[] keys = { "W", "D", "S", "A" };
            Color[] colors = {
                new Color(0, 1, 1),
                new Color(1, 0, 1),
                new Color(1, 1, 0),
                new Color(0, 1, 0.5f)
            };
            Vector2[] offsets = {
                new Vector2(0, -44),
                new Vector2(44, 0),
                new Vector2(0, 44),
                new Vector2(-44, 0)
            };

            for (int i = 0; i < 4; i++)
            {
                var lbl = MakeLabel($"{symbols[i]}\n{keys[i]}", 20, colors[i]);
                lbl.Position = new Vector2(bx + offsets[i].X - 20, by + offsets[i].Y - 16);
                lbl.Size = new Vector2(40, 36);
                lbl.HorizontalAlignment = HorizontalAlignment.Center;
                _arrows[i] = lbl;
            }

            var center = MakeLabel("\u25cf", 16, new Color(0.3f, 0.3f, 0.4f));
            center.Position = new Vector2(bx - 10, by - 10);
            center.Size = new Vector2(20, 20);
            center.HorizontalAlignment = HorizontalAlignment.Center;
        }

        private Label MakeLabel(string text, int fontSize, Color color)
        {
            var lbl = new Label();
            lbl.Text = text;
            lbl.AddThemeFontSizeOverride("font_size", fontSize);
            lbl.AddThemeColorOverride("font_color", color);
            _ui.AddChild(lbl);
            return lbl;
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;

            for (int i = 0; i < 4; i++)
            {
                if (_arrowFlash[i] > 0)
                {
                    _arrowFlash[i] -= dt * 4f;
                    float a = Math.Max(0.4f, 1.0f - _arrowFlash[i]);
                    _arrows[i].Modulate = new Color(1, 1, 1, a + _arrowFlash[i] * 0.6f);
                }
            }

            if (_orbMesh != null)
            {
                float bob = MathF.Sin((float)Time.GetTicksMsec() / 400f) * 0.04f;
                var pos = _orbMesh.Position;
                if (_phase != Phase.Sliding)
                    _orbMesh.Position = new Vector3(pos.X, OrbY + bob, pos.Z);
            }

            switch (_phase)
            {
                case Phase.Idle:
                    HandleInput();
                    break;
                case Phase.Sliding:
                    UpdateSlide(dt);
                    break;
                case Phase.Complete:
                    if (Input.IsActionJustPressed("jump") || Input.IsPhysicalKeyPressed(Key.Space))
                    {
                        _totalScore += _logic.Score + _logic.CompletionBonus();
                        if (_currentLevel < PikuiLogic.MaxLevels)
                            StartLevel(_currentLevel + 1);
                        else
                        {
                            _phase = Phase.AllDone;
                            _messageLabel.Text = $"GAME COMPLETE!\nTOTAL: {_totalScore + _logic.Score + _logic.CompletionBonus()}";
                            _messageLabel.Visible = true;
                        }
                    }
                    break;
                case Phase.AllDone:
                    if (Input.IsPhysicalKeyPressed(Key.R))
                    {
                        _totalScore = 0;
                        StartLevel(1);
                    }
                    break;
            }

            if (Input.IsPhysicalKeyPressed(Key.R) && _phase != Phase.Sliding && _phase != Phase.AllDone)
            {
                StartLevel(_currentLevel);
            }
        }

        private void HandleInput()
        {
            SlideDirection? dir = null;
            int arrowIdx = -1;

            if (Input.IsActionJustPressed("move_forward"))  { dir = SlideDirection.Up; arrowIdx = 0; }
            else if (Input.IsActionJustPressed("move_right"))  { dir = SlideDirection.Right; arrowIdx = 1; }
            else if (Input.IsActionJustPressed("move_backward")) { dir = SlideDirection.Down; arrowIdx = 2; }
            else if (Input.IsActionJustPressed("move_left"))   { dir = SlideDirection.Left; arrowIdx = 3; }

            if (dir == null) return;

            if (arrowIdx >= 0) _arrowFlash[arrowIdx] = 1.0f;

            var path = _logic.Slide(dir.Value);

            if (path.Count == 0)
            {
                PlaySfx(PikuiSounds.GenerateBump());
                UpdateUI();
                return;
            }

            _slidePath = path;
            _slideIndex = 0;
            _slideElapsed = 0;
            _slideDuration = Math.Max(path.Count * SlideTimePerTile, 0.12f);
            _slideFrom = _orbMesh.Position;
            var (fx, fy, _) = path[^1];
            _slideTo = GridToWorld(fx, fy) + new Vector3(0, OrbY, 0);
            _slideDir = dir.Value;
            _phase = Phase.Sliding;
        }

        private void UpdateSlide(float dt)
        {
            _slideElapsed += dt;

            int reached = (int)(_slideElapsed / SlideTimePerTile);
            while (_slideIndex < _slidePath.Count && _slideIndex < reached)
            {
                var (tx, ty, wasNew) = _slidePath[_slideIndex];
                var color = PikuiLogic.DirectionToColor(_slideDir);
                PaintTileVisual(tx, ty, color);

                if (wasNew)
                {
                    float freq = PikuiSounds.GetTileFrequency(tx, ty, _logic.GridSize);
                    var wf = PikuiSounds.DirectionWaveform(_slideDir);
                    PlaySfx(PikuiSounds.GenerateNote(freq, wf));
                }

                _slideIndex++;
            }

            float t = Mathf.Clamp(_slideElapsed / _slideDuration, 0, 1);
            t = 1f - (1f - t) * (1f - t); // ease out quad
            _orbMesh.Position = _slideFrom.Lerp(_slideTo, t);

            if (_slideElapsed >= _slideDuration)
            {
                _orbMesh.Position = _slideTo;
                _phase = Phase.Idle;
                UpdateUI();
                UpdateAmbient();

                if (_logic.IsComplete)
                {
                    _phase = Phase.Complete;
                    int bonus = _logic.CompletionBonus();
                    string bonusText = bonus > 0 ? $"\nBONUS: +{bonus}" : "";
                    _messageLabel.Text = _currentLevel < PikuiLogic.MaxLevels
                        ? $"LEVEL COMPLETE!{bonusText}\n\nPress SPACE"
                        : $"FINAL LEVEL COMPLETE!{bonusText}\n\nPress SPACE";
                    _messageLabel.Visible = true;
                    PlaySfx(PikuiSounds.GenerateVictory(), -3);
                    FlashAllTiles();
                }
            }
        }

        private void PaintTileVisual(int x, int y, PaintColor color)
        {
            var tile = _tileMeshes[x, y];
            if (tile == null) return;

            var (albedo, emission) = color switch
            {
                PaintColor.Cyan => (new Color(0.05f, 0.5f, 0.5f), new Color(0, 1, 1)),
                PaintColor.Magenta => (new Color(0.5f, 0.05f, 0.5f), new Color(1, 0, 1)),
                PaintColor.Yellow => (new Color(0.5f, 0.5f, 0.05f), new Color(1, 1, 0)),
                PaintColor.Green => (new Color(0.05f, 0.5f, 0.25f), new Color(0, 1, 0.5f)),
                _ => (new Color(0.07f, 0.07f, 0.09f), new Color(0, 0, 0))
            };

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = albedo;
            mat.Metallic = 0.15f;
            mat.Roughness = 0.7f;
            if (color != PaintColor.None)
            {
                mat.EmissionEnabled = true;
                mat.Emission = emission;
                mat.EmissionEnergyMultiplier = 1.5f;
            }
            tile.MaterialOverride = mat;
        }

        private void FlashAllTiles()
        {
            int size = _logic.GridSize;
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    var tile = _tileMeshes[x, y];
                    if (tile?.MaterialOverride is StandardMaterial3D m && m.EmissionEnabled)
                    {
                        var tween = CreateTween();
                        tween.TweenProperty(m, "emission_energy_multiplier", 3.0f, 0.3f);
                        tween.TweenProperty(m, "emission_energy_multiplier", 1.5f, 0.5f);
                    }
                }
            }
        }

        private void UpdateUI()
        {
            _levelLabel.Text = $"LEVEL {_currentLevel}";
            _scoreLabel.Text = $"SCORE: {_logic.Score + _totalScore}";
            _movesLabel.Text = $"MOVES: {_logic.Moves}";
            _parLabel.Text = $"PAR: {_logic.Par}";

            if (_logic.Combo > 1)
            {
                _comboLabel.Text = $"COMBO x{_logic.Combo}";
                _comboLabel.Visible = true;
            }
            else
            {
                _comboLabel.Visible = false;
            }

            float pct = _logic.Progress * 100;
            _progressBar.Value = pct;
            _progressText.Text = $"{(int)pct}%";

            Color fillColor = pct switch
            {
                >= 90 => new Color(0.2f, 1, 0.4f),
                >= 60 => new Color(0.3f, 0.8f, 1),
                >= 30 => new Color(0.8f, 0.6f, 1),
                _ => new Color(0.5f, 0.5f, 0.7f)
            };
            var fill = new StyleBoxFlat();
            fill.BgColor = fillColor;
            fill.CornerRadiusBottomLeft = 4;
            fill.CornerRadiusBottomRight = 4;
            fill.CornerRadiusTopLeft = 4;
            fill.CornerRadiusTopRight = 4;
            _progressBar.AddThemeStyleboxOverride("fill", fill);
        }

        private void UpdateAmbient()
        {
            float prog = _logic.Progress;
            if (Math.Abs(prog - _ambientProgress) < 0.05f) return;
            _ambientProgress = prog;

            var data = PikuiSounds.GenerateAmbientLoop(prog);
            var stream = MakeStream(data);
            stream.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
            stream.LoopEnd = data.Length / 2;
            _ambientPlayer.Stream = stream;
            _ambientPlayer.Play();
        }

        private void PlaySfx(byte[] pcmData, float volumeDb = 0)
        {
            var player = new AudioStreamPlayer();
            _levelRoot.AddChild(player);
            player.Stream = MakeStream(pcmData);
            player.VolumeDb = volumeDb;
            player.Play();
            player.Finished += () => player.QueueFree();
        }

        private static AudioStreamWav MakeStream(byte[] data)
        {
            var s = new AudioStreamWav();
            s.Format = AudioStreamWav.FormatEnum.Format16Bits;
            s.MixRate = PikuiSounds.GetSampleRate();
            s.Stereo = false;
            s.Data = data;
            return s;
        }

        private Vector3 GridToWorld(int gx, int gy)
        {
            int size = _logic.GridSize;
            float cx = (gx - size / 2.0f + 0.5f) * TileSpacing;
            float cz = (gy - size / 2.0f + 0.5f) * TileSpacing;
            return new Vector3(cx, 0, cz);
        }
    }
}
