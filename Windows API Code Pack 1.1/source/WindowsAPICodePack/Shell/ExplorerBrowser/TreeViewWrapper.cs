﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using WindowsHelper;

namespace Microsoft.WindowsAPICodePack.Shell.ExplorerBrowser {
  public class TreeViewWrapper : IDisposable {
    public delegate void TreeViewMiddleClickedHandler(IShellItem item);
    public delegate bool FolderClickedHandler(ShellObject item, Keys modkeys, bool middle);
    public event FolderClickedHandler TreeViewClicked;

    private bool fDisposed;
    private WindowsAPI.INameSpaceTreeControl treeControl;
    private NativeWindowController treeController;
    private NativeWindowController parentController;
    private bool fPreventSelChange;

    public TreeViewWrapper(IntPtr hwnd, WindowsAPI.INameSpaceTreeControl treeControl) {
      this.treeControl = treeControl;
      treeController = new NativeWindowController(hwnd);
      treeController.MessageCaptured += TreeControl_MessageCaptured;
      parentController = new NativeWindowController(WindowsAPI.GetParent(hwnd));
      parentController.MessageCaptured += ParentControl_MessageCaptured;
    }

    private bool HandleClick(Point pt, Keys modifierKeys, bool middle) {
      IShellItem item = null;
      try {
        WindowsAPI.TVHITTESTINFO structure = new WindowsAPI.TVHITTESTINFO { pt = pt };
        IntPtr wParam = WindowsAPI.SendMessage(treeController.Handle, 0x1111, IntPtr.Zero, ref structure);
        if (wParam != IntPtr.Zero) {
          if ((structure.flags & 0x10) == 0 && (structure.flags & 0x80) == 0) {
            treeControl.HitTest(pt, out item);
            if (item != null) {
              ShellObject obj = ShellObjectFactory.Create(item);
                TreeViewClicked(obj, modifierKeys, middle);
            }
          }
        }
      } finally {
        if (item != null) {
          Marshal.ReleaseComObject(item);
        }
      }
      return false;
    }

    private bool TreeControl_MessageCaptured(ref Message msg) {
      switch (msg.Msg) {
        case (int)WindowsAPI.WndMsg.WM_USER:
          fPreventSelChange = false;
          break;

        case (int)WindowsAPI.WndMsg.WM_MBUTTONUP:
          if (treeControl != null && TreeViewClicked != null) {
            HandleClick(WindowsAPI.PointFromLPARAM(msg.LParam), Control.ModifierKeys, true);
          }
          break;

        case (int)WindowsAPI.WndMsg.WM_DESTROY:
          if (treeControl != null) {
            Marshal.ReleaseComObject(treeControl);
            treeControl = null;
          }
          break;
      }
      return false;
    }

    private bool ParentControl_MessageCaptured(ref Message msg) {
      if (msg.Msg == (int)WindowsAPI.WndMsg.WM_NOTIFY) {
        WindowsAPI.NMHDR nmhdr = (WindowsAPI.NMHDR)Marshal.PtrToStructure(msg.LParam, typeof(WindowsAPI.NMHDR));
        switch (nmhdr.code) {
          case -2: /* NM_CLICK */
            if (Control.ModifierKeys != Keys.None) {
              Point pt = Control.MousePosition;
              WindowsAPI.ScreenToClient(nmhdr.hwndFrom, ref pt);
              if (HandleClick(pt, Control.ModifierKeys, false)) {
                fPreventSelChange = true;
                WindowsAPI.PostMessage(nmhdr.hwndFrom, (int)WindowsAPI.WndMsg.WM_USER, IntPtr.Zero, IntPtr.Zero);
                return true;
              }
            }
            break;

          case -450: /* TVN_SELECTIONCHANGING */
            if (fPreventSelChange) {
              msg.Result = (IntPtr)1;
              return true;
            }
            break;
        }
      }
      return false;
    }

    #region IDisposable Members

    public void Dispose() {
      if (fDisposed) return;
      if (treeControl != null) {
        Marshal.ReleaseComObject(treeControl);
        treeControl = null;
      }
      fDisposed = true;
    }

    #endregion
  }
}