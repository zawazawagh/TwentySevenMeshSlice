using UnityEngine;

namespace TwentySevenMeshSlice
{
    [RequireComponent(typeof(MeshFilter))]
    [DisallowMultipleComponent]
    public class TwentySevenMeshSlice : MonoBehaviour
    {
        //margin between inner and outer box
        private static readonly float s_margin = 0.0001f;

        [SerializeField]
        private Mesh _originalMesh;

        //"I" stands for "Inner"
        [SerializeField]
        private float _xMinI, _xMaxI, _yMinI, _yMaxI, _zMinI, _zMaxI;

        //"O" stands for "Outer"
        [SerializeField]
        private float _xMinO, _xMaxO, _yMinO, _yMaxO, _zMinO, _zMaxO;

        [SerializeField]
        private Mesh _slicedMesh;

        private Vector3[] _verts;
        private Vector3Int[] _vertexSectorIds;
        private Vector3[] _vertexDistanceRatios;
        private Vector3[] _vertexDistances;

        public MeshFilter MeshFilter => GetComponent<MeshFilter>();

        private void Awake()
        {
            Init();
            _verts = _slicedMesh.vertices;
            DefineVertices();
        }

        private void OnValidate()
        {
            if (MeshFilter.sharedMesh != null && _slicedMesh == null || _slicedMesh != MeshFilter.sharedMesh)
            {
                DestroyImmediate(_slicedMesh);
                Init();
            }

            ValidateInnerBoxBound();
        }

        public Vector3 GetInnerBoxSize() => new Vector3(_xMaxI - _xMinI, _yMaxI - _yMinI, _zMaxI - _zMinI);

        public Vector3 GetInnerBoxCenter() => new Vector3(_xMaxI + _xMinI, _yMaxI + _yMinI, _zMaxI + _zMinI) / 2;

        public Vector3 GetOuterBoxSize() => new Vector3(_xMaxO - _xMinO, _yMaxO - _yMinO, _zMaxO - _zMinO);

        public Vector3 GetOuterBoxCenter() => new Vector3(_xMaxO + _xMinO, _yMaxO + _yMinO, _zMaxO + _zMinO) / 2;

        public void SetInnerBoxSize(Vector3 size, Vector3 center)
        {
            Vector3 minBounds = center - size / 2;
            Vector3 maxBounds = center + size / 2;
            _xMinI = minBounds.x;
            _yMinI = minBounds.y;
            _zMinI = minBounds.z;
            _xMaxI = maxBounds.x;
            _yMaxI = maxBounds.y;
            _zMaxI = maxBounds.z;
            ValidateInnerBoxBound();
        }

        public void SetOuterBoxSize(Vector3 size, Vector3 center)
        {
            Vector3 minBounds = center - size / 2;
            Vector3 maxBounds = center + size / 2;
            _xMinO = minBounds.x;
            _yMinO = minBounds.y;
            _zMinO = minBounds.z;
            _xMaxO = maxBounds.x;
            _yMaxO = maxBounds.y;
            _zMaxO = maxBounds.z;
            ValidateInnerBoxBound();
        }

        public void SetEntireSize(Vector3 newOuterBoxSize, Vector3 _)
        {
            var moveAmount = (newOuterBoxSize - GetOuterBoxSize()) / 2;
            (_xMinO, _xMinI, _xMaxI, _xMaxO) = (_xMinO - moveAmount.x, _xMinI - moveAmount.x, _xMaxI + moveAmount.x, _xMaxO + moveAmount.x);
            (_yMinO, _yMinI, _yMaxI, _yMaxO) = (_yMinO - moveAmount.y, _yMinI - moveAmount.y, _yMaxI + moveAmount.y, _yMaxO + moveAmount.y);
            (_zMinO, _zMinI, _zMaxI, _zMaxO) = (_zMinO - moveAmount.z, _zMinI - moveAmount.z, _zMaxI + moveAmount.z, _zMaxO + moveAmount.z);
            ValidateInnerBoxBound();
        }

        public void Init()
        {
            if (_originalMesh == null)
            {
                return;
            }

            _slicedMesh = new Mesh
            {
                name = $"{_originalMesh.name}_27sliced",
                vertices = _originalMesh.vertices,
                normals = _originalMesh.normals,
                colors = _originalMesh.colors,
                uv = _originalMesh.uv,
                triangles = _originalMesh.triangles,
                tangents = _originalMesh.tangents
            };

            int sub = _originalMesh.subMeshCount;
            _verts = _slicedMesh.vertices;
            _slicedMesh.subMeshCount = sub;
            for (var i = 0; i < sub; i++) {
                _slicedMesh.SetTriangles(_originalMesh.GetTriangles(i), i);
            }

            MeshFilter.sharedMesh = _slicedMesh;
        }

        public void UpdateMeshScale()
        {
            if (_slicedMesh == null || _originalMesh == null)
            {
                return;
            }

            for (var i = 0; i < _verts.Length; i++)
            {
                Vector3 newVertexPos = default;
                //keep the same distance ratio between inner box and outer box
                Vector3Int sectorId = _vertexSectorIds[i];
                Vector3 distanceRatio = _vertexDistanceRatios[i];
                Vector3 distanceFromOuterBox = _vertexDistances[i];
                newVertexPos.x = GetAbsolutePositionFromSectorId(sectorId.x,
                                                                 distanceRatio.x,
                                                                 distanceFromOuterBox.x,
                                                                 _xMinI,
                                                                 _xMaxI,
                                                                 _xMinO,
                                                                 _xMaxO);
                newVertexPos.y = GetAbsolutePositionFromSectorId(sectorId.y,
                                                                 distanceRatio.y,
                                                                 distanceFromOuterBox.y,
                                                                 _yMinI,
                                                                 _yMaxI,
                                                                 _yMinO,
                                                                 _yMaxO);
                newVertexPos.z = GetAbsolutePositionFromSectorId(sectorId.z,
                                                                 distanceRatio.z,
                                                                 distanceFromOuterBox.z,
                                                                 _zMinI,
                                                                 _zMaxI,
                                                                 _zMinO,
                                                                 _zMaxO);
                _verts[i] = newVertexPos;
            }

            _slicedMesh.vertices = _verts;
        }

        public void DefineVertices()
        {
            var vertexCount = _slicedMesh.vertexCount;
            _vertexSectorIds = new Vector3Int[vertexCount];
            _vertexDistanceRatios = new Vector3[vertexCount];
            _vertexDistances = new Vector3[vertexCount];
            for (var i = 0; _slicedMesh != null && i < vertexCount; i++)
            {
                Vector3 vert = _slicedMesh.vertices[i];
                (Vector3Int sectorId, Vector3 distanceRatio, Vector3 outerBoxDistance) = DefineVertex(vert);
                _vertexSectorIds[i] = sectorId;
                _vertexDistanceRatios[i] = distanceRatio;
                _vertexDistances[i] = outerBoxDistance;
            }

            (Vector3Int, Vector3, Vector3) DefineVertex(Vector3 vertPos)
            {
                (int sectorId, float distanceRatio, float outerBoxDistance) x = DefineComponent(vertPos.x, _xMinI, _xMaxI, _xMinO, _xMaxO);
                (int sectorId, float distanceRatio, float outerBoxDistance) y = DefineComponent(vertPos.y, _yMinI, _yMaxI, _yMinO, _yMaxO);
                (int sectorId, float distanceRatio, float outerBoxDistance) z = DefineComponent(vertPos.z, _zMinI, _zMaxI, _zMinO, _zMaxO);
                return (
                    new Vector3Int(x.sectorId, y.sectorId, z.sectorId),
                    new Vector3(x.distanceRatio, y.distanceRatio, z.distanceRatio),
                    new Vector3(x.outerBoxDistance, y.outerBoxDistance, z.outerBoxDistance)
                    );
            }

            (int, float, float) DefineComponent(float value, float minValueI, float maxValueI, float minValueO, float maxValueO)
            {
                int sectorId = default;
                float distanceRatio = default;
                float outerBoxDistance = default;
                if (value <= minValueI)
                {
                    sectorId = 0;
                    distanceRatio = (value - minValueO) / (minValueI - minValueO);
                    outerBoxDistance = Mathf.Abs(value - minValueO);
                }
                else if (value > minValueI && value <= maxValueI)
                {
                    sectorId = 1;
                    distanceRatio = (value - minValueI) / (maxValueI - minValueI);
                }
                else if (value > maxValueI)
                {
                    sectorId = 2;
                    distanceRatio = (value - maxValueI) / (maxValueO - maxValueI);
                    outerBoxDistance = Mathf.Abs(value - maxValueO);
                }

                return (sectorId, distanceRatio, outerBoxDistance);
            }
        }

        private float GetAbsolutePositionFromSectorId(int id,
                                                      float distanceRatio,
                                                      float outerBoxDistance,
                                                      float minValueI,
                                                      float maxValueI,
                                                      float minValueO,
                                                      float maxValueO)
        {
            switch (id)
            {
                case 0:
                    if (distanceRatio < 0)
                    {
                        return minValueO - outerBoxDistance;
                    }
                    else
                    {
                        return (minValueI - minValueO) * distanceRatio + minValueO;
                    }
                case 1:
                    return (maxValueI - minValueI) * distanceRatio + minValueI;
                case 2:
                    if (distanceRatio > 1)
                    {
                        return maxValueO + outerBoxDistance;
                    }
                    else
                    {
                        return (maxValueO - maxValueI) * distanceRatio + maxValueI;
                    }
                default:
                    return default;
            }
        }

        private  void ValidateInnerBoxBound()
        {
            _xMinI = Mathf.Max(_xMinI, _xMinO + s_margin);
            _yMinI = Mathf.Max(_yMinI, _yMinO + s_margin);
            _zMinI = Mathf.Max(_zMinI, _zMinO + s_margin);
            _xMaxI = Mathf.Min(_xMaxI, _xMaxO - s_margin);
            _yMaxI = Mathf.Min(_yMaxI, _yMaxO - s_margin);
            _zMaxI = Mathf.Min(_zMaxI, _zMaxO - s_margin);
        }
    }
}
