using System.Collections.Generic;
using UnityEngine;

// =====================================================================
//  KUBIKA TOWER - BALOK MEMBULAT (rounded / beveled) ala BlockBlast
// ---------------------------------------------------------------------
//  ADDITIVE: cuma menyediakan mesh "rounded cube" (kubus satuan bersudut
//  membulat) yang dipakai ulang oleh MakeBlock (di Tetris3D.cs). Tujuannya
//  supaya balok tidak terasa terlalu kotak: tepi & sudut dibulatkan, tapi
//  bagian tengah tiap sisi tetap datar (mengkilap saat kena bloom).
//
//  Cara kerja pembulatan: tiap titik permukaan kubus tajam di-"clamp" ke
//  kotak-dalam (half - radius), lalu didorong keluar sejauh radius searah
//  selisihnya. Hasilnya sisi datar tetap datar, tepi & sudut jadi lengkung
//  mulus dengan normal yang benar (shading halus, tanpa celah antar-sisi).
// =====================================================================
public partial class Tetris3D
{
    static Mesh kbRoundedCube;

    // Mesh kubus satuan (ukuran 1, half=0.5) bersudut membulat. Dibuat SEKALI
    // lalu dipakai bersama semua balok (di-scale lewat transform.localScale),
    // jadi tak menambah biaya memori per-balok.
    Mesh RoundedBlockMesh()
    {
        if (kbRoundedCube == null)
            kbRoundedCube = BuildRoundedCube(0.5f, 0.15f, 6);
        return kbRoundedCube;
    }

    // half   = setengah ukuran kubus (0.5 untuk kubus satuan)
    // radius = jari-jari lengkung sudut/tepi (0..half). Makin besar makin bulat.
    // seg    = subdivisi per sisi. Makin besar makin halus lengkungannya.
    static Mesh BuildRoundedCube(float half, float radius, int seg)
    {
        radius = Mathf.Clamp(radius, 0.001f, half);
        if (seg < 1) seg = 1;
        float inner = half - radius;

        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var tris  = new List<int>();

        // 6 sisi kubus: (arah normal, sumbu-u, sumbu-v). Kombinasi u/v dipilih
        // supaya winding segitiga menghasilkan muka menghadap keluar.
        Vector3[] faceN = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        Vector3[] faceU = { Vector3.forward, Vector3.back, Vector3.right, Vector3.right, Vector3.left, Vector3.right };
        Vector3[] faceV = { Vector3.up, Vector3.up, Vector3.forward, Vector3.back, Vector3.up, Vector3.up };

        for (int f = 0; f < 6; f++)
        {
            Vector3 n = faceN[f];
            Vector3 u = faceU[f];
            Vector3 v = faceV[f];
            int baseIdx = verts.Count;
            int row = seg + 1;

            for (int iy = 0; iy <= seg; iy++)
            {
                float ty = (float)iy / seg * 2f - 1f;   // -1..1
                for (int ix = 0; ix <= seg; ix++)
                {
                    float tx = (float)ix / seg * 2f - 1f;   // -1..1
                    // titik di permukaan kubus TAJAM (ukuran half)
                    Vector3 p = n * half + u * (tx * half) + v * (ty * half);
                    // proyeksi membulat
                    Vector3 clamped = new Vector3(
                        Mathf.Clamp(p.x, -inner, inner),
                        Mathf.Clamp(p.y, -inner, inner),
                        Mathf.Clamp(p.z, -inner, inner));
                    Vector3 dir = p - clamped;
                    Vector3 nrm = (dir.sqrMagnitude > 1e-6f) ? dir.normalized : n;
                    verts.Add(clamped + nrm * radius);
                    norms.Add(nrm);
                }
            }

            for (int iy = 0; iy < seg; iy++)
                for (int ix = 0; ix < seg; ix++)
                {
                    int a = baseIdx + iy * row + ix;
                    int b = a + 1;
                    int c = a + row;
                    int d = c + 1;
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
        }

        var m = new Mesh();
        m.name = "KubikaRoundedCube";
        m.indexFormat = (verts.Count > 65000)
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;
        m.SetVertices(verts);
        m.SetNormals(norms);
        m.SetTriangles(tris, 0);
        m.RecalculateBounds();
        m.RecalculateTangents();
        return m;
    }
}
