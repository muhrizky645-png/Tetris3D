using UnityEngine;

// =====================================================================
//  KUBIKA TOWER - POSISI KONTROL GAMEPLAY
//  Default: tombol di kanan, inventaris buff di sisi berlawanan.
// =====================================================================
public partial class Tetris3D
{
    const string PP_CONTROL_SIDE = "kubika_control_side";
    bool controlSideLoaded;
    bool controlsOnRight = true;

    void EnsureControlSide()
    {
        if (controlSideLoaded) return;
        controlSideLoaded = true;
        // 1 = kanan (default), 0 = kiri.
        controlsOnRight = PlayerPrefs.GetInt(PP_CONTROL_SIDE, 1) == 1;
    }

    public bool ControlsOnRight
    {
        get { EnsureControlSide(); return controlsOnRight; }
    }

    public bool BuffsOnRight
    {
        get { return !ControlsOnRight; }
    }

    public string ControlSideLabel
    {
        get
        {
            EnsureControlSide();
            if (SalID) return controlsOnRight ? "Kontrol: KANAN" : "Kontrol: KIRI";
            return controlsOnRight ? "Controls: RIGHT" : "Controls: LEFT";
        }
    }

    public void ToggleControlSide()
    {
        EnsureControlSide();
        controlsOnRight = !controlsOnRight;
        PlayerPrefs.SetInt(PP_CONTROL_SIDE, controlsOnRight ? 1 : 0);
        PlayerPrefs.Save();
    }
}
