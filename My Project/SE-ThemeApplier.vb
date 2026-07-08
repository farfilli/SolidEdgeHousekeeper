' ============================================================
'  SE-ThemeApplier.vb - Applica il tema (SE-Theme.vb) a un form
'  costruito col designer, SENZA modificare il designer stesso.
'
'  Uso (nel Load del form, DOPO l'inizializzazione dell'app):
'      UiTheme.SetTheme(True)                    ' True = dark, False = light
'      ThemeApplier.Apply(Me)
'      ThemeApplier.ApplyToolStrip(MioContextMenu)   ' i ContextMenuStrip non
'                                                    ' stanno nell'albero Controls
'
'  Cosa fa per tipo di controllo:
'    Button                Flat + colori tema (hover/pressed inclusi)
'    CheckBox/RadioButton  testo a tema (il glifo resta di sistema)
'    Label                 testo a tema
'    TextBox               campo scuro, bordo singolo, scrollbar scura se multiline
'    ComboBox              flat scuro
'    GroupBox              bordo e titolo ridisegnati a tema
'    TabControl            linguette owner-draw a tema
'    ListView/ListBox/     colori + scrollbar native a tema
'    CheckedListBox        (l'header del ListView resta di sistema)
'    ToolStrip/MenuStrip/  renderer professionale con palette del tema
'    StatusStrip/ContextMenuStrip
'    Panel/TabPage/Split.. sfondi a tema
' ============================================================

Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Runtime.InteropServices
Imports System.Windows.Forms

Public Module ThemeApplier

    Private ReadOnly ownerDrawnTabs As New HashSet(Of TabControl)
    Private ReadOnly paintedGroups As New HashSet(Of GroupBox)
    Private ReadOnly borderFixedControls As New HashSet(Of Control)
    Private ReadOnly nativeFixes As New List(Of NativeWindow)
    Private sharedRenderer As UiThemeToolStripRenderer = Nothing

    Private cachedChecked As Bitmap = Nothing
    Private cachedUnchecked As Bitmap = Nothing
    Private cachedImagesDark As Boolean
    Private prevChecked As Bitmap = Nothing
    Private prevUnchecked As Bitmap = Nothing

    ' Immagini 16x16 di spunta generate dalla palette: sostituiscono le
    ' risorse bianche (es. My.Resources.Checked/Unchecked).
    Public ReadOnly Property CheckedImage As Image
        Get
            EnsureCheckImages()
            Return cachedChecked
        End Get
    End Property

    Public ReadOnly Property UncheckedImage As Image
        Get
            EnsureCheckImages()
            Return cachedUnchecked
        End Get
    End Property

    Private Sub EnsureCheckImages()
        If cachedChecked IsNot Nothing AndAlso cachedImagesDark = UiTheme.IsDark Then Return

        cachedImagesDark = UiTheme.IsDark
        cachedChecked = New Bitmap(16, 16)
        cachedUnchecked = New Bitmap(16, 16)

        Using g As Graphics = Graphics.FromImage(cachedUnchecked)
            g.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias
            g.Clear(Color.Transparent)
            Using bb As New SolidBrush(UiTheme.BgField)
                g.FillRectangle(bb, 1, 1, 13, 13)
            End Using
            Using bp As New Pen(UiTheme.Border, 1.0F)
                g.DrawRectangle(bp, 1, 1, 13, 13)
            End Using
        End Using

        Using g As Graphics = Graphics.FromImage(cachedChecked)
            g.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias
            g.Clear(Color.Transparent)
            Using ab As New SolidBrush(UiTheme.Accent)
                g.FillRectangle(ab, 1, 1, 13, 13)
            End Using
            Using bp As New Pen(UiTheme.Border, 1.0F)
                g.DrawRectangle(bp, 1, 1, 13, 13)
            End Using
            Using cp As New Pen(UiTheme.BgSidebar, 2.0F)
                g.DrawLine(cp, 4, 8, 7, 11)
                g.DrawLine(cp, 7, 11, 12, 5)
            End Using
        End Using
    End Sub

    <DllImport("user32.dll")>
    Private Function SendMessageW(hWnd As IntPtr, msg As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr
    End Function

    Friend Declare Function GetWindowDC Lib "user32.dll" (hWnd As IntPtr) As IntPtr
    Friend Declare Function GetScrollPos Lib "user32.dll" (hWnd As IntPtr, nBar As Integer) As Integer
    Friend Declare Function ReleaseDC Lib "user32.dll" (hWnd As IntPtr, hdc As IntPtr) As Integer

    Public Sub Apply(root As Form)
        ' Se il tema e' cambiato, le nuove immagini di spunta vanno rimappate
        ' sui controlli che tengono ancora il riferimento a quelle vecchie.
        Dim oldC As Bitmap = cachedChecked
        Dim oldU As Bitmap = cachedUnchecked
        EnsureCheckImages()
        prevChecked = If(oldC IsNot cachedChecked, oldC, Nothing)
        prevUnchecked = If(oldU IsNot cachedUnchecked, oldU, Nothing)

        root.BackColor = UiTheme.BgSidebar
        root.ForeColor = UiTheme.Txt

        WalkAndTheme(root)

        If root.IsHandleCreated Then
            UiTheme.ApplyTitleBarTheme(root.Handle)
        End If
    End Sub

    Private Sub WalkAndTheme(parent As Control)
        For Each c As Control In parent.Controls
            ThemeControl(c)
            WalkAndTheme(c)
        Next
    End Sub

    Private Sub ThemeControl(c As Control)
        If TypeOf c Is Button Then
            Dim b = DirectCast(c, Button)
            b.FlatStyle = FlatStyle.Flat
            b.UseVisualStyleBackColor = False
            b.BackColor = UiTheme.BgField
            b.ForeColor = UiTheme.Txt
            b.FlatAppearance.BorderColor = UiTheme.Border
            b.FlatAppearance.BorderSize = 1
            b.FlatAppearance.MouseOverBackColor = UiTheme.BgFieldHi
            b.FlatAppearance.MouseDownBackColor = UiTheme.Border

        ElseIf TypeOf c Is CheckBox OrElse TypeOf c Is RadioButton Then
            ' FlatStyle.Flat: il glifo viene disegnato piatto con i colori del
            ' controllo invece del rendering di sistema (bianco/grigio); poi
            ' viene ridipinto a tema nel Paint (solo se non usa un'immagine).
            Dim bb = TryCast(c, ButtonBase)
            If bb IsNot Nothing Then
                bb.FlatStyle = FlatStyle.Flat
                If prevChecked IsNot Nothing AndAlso bb.Image Is prevChecked Then bb.Image = cachedChecked
                If prevUnchecked IsNot Nothing AndAlso bb.Image Is prevUnchecked Then bb.Image = cachedUnchecked
            End If
            c.ForeColor = UiTheme.Txt
            c.BackColor = Color.Transparent
            If bb IsNot Nothing AndAlso bb.Image Is Nothing AndAlso borderFixedControls.Add(c) Then
                AddHandler c.Paint, AddressOf CheckGlyph_PaintThemed
            End If

        ElseIf TypeOf c Is Label Then
            c.ForeColor = UiTheme.Txt
            c.BackColor = Color.Transparent

        ElseIf TypeOf c Is TextBox Then
            Dim t = DirectCast(c, TextBox)
            t.BackColor = UiTheme.BgField
            t.ForeColor = UiTheme.Txt
            t.BorderStyle = BorderStyle.FixedSingle
            If borderFixedControls.Add(t) Then
                nativeFixes.Add(New NcBorderFix(t))
            End If
            If t.Multiline AndAlso t.IsHandleCreated Then
                UiTheme.ApplyScrollBarTheme(t.Handle)
            End If

        ElseIf TypeOf c Is ComboBox Then
            Dim cb = DirectCast(c, ComboBox)
            cb.FlatStyle = FlatStyle.Flat
            cb.BackColor = UiTheme.BgField
            cb.ForeColor = UiTheme.Txt

        ElseIf TypeOf c Is GroupBox Then
            Dim gb = DirectCast(c, GroupBox)
            gb.BackColor = UiTheme.BgSidebar
            gb.ForeColor = UiTheme.Txt
            If paintedGroups.Add(gb) Then
                AddHandler gb.Paint, AddressOf GroupBox_PaintThemed
            End If

        ElseIf TypeOf c Is TabControl Then
            Dim tc = DirectCast(c, TabControl)
            tc.DrawMode = TabDrawMode.OwnerDrawFixed
            If tc.ItemSize.Height < 24 Then
                tc.ItemSize = New Size(tc.ItemSize.Width, 24)
            End If
            If ownerDrawnTabs.Add(tc) Then
                AddHandler tc.DrawItem, AddressOf TabControl_DrawItemThemed
                ' La cornice 3D attorno alla pagina e' dipinta dal controllo con
                ' colori di sistema: viene coperta dopo ogni WM_PAINT.
                nativeFixes.Add(New TabFrameFix(tc))
            End If
            tc.Invalidate()

        ElseIf TypeOf c Is ListView Then
            Dim lv = DirectCast(c, ListView)
            lv.BackColor = UiTheme.BgField
            lv.ForeColor = UiTheme.Txt
            lv.BorderStyle = BorderStyle.FixedSingle
            ' Le GridLines di sistema non sono ricolorabili (linee bianche su
            ' fondo scuro): spente; nelle liste owner-draw le righe di griglia
            ' vengono ridisegnate a tema nel DrawSubItem.
            lv.GridLines = False
            If lv.IsHandleCreated Then
                UiTheme.ApplyScrollBarTheme(lv.Handle)
                ' L'area dell'header oltre l'ultima colonna e' dipinta dal
                ' controllo header di sistema: tema scuro nativo.
                Const LVM_GETHEADER As Integer = &H101F
                Dim hHeader As IntPtr = SendMessageW(lv.Handle, LVM_GETHEADER, IntPtr.Zero, IntPtr.Zero)
                If hHeader <> IntPtr.Zero Then
                    UiTheme.ApplyScrollBarTheme(hHeader)
                    ' Il riempimento oltre l'ultima colonna resta bianco anche
                    ' col tema nativo: coperto dopo ogni WM_PAINT dell'header.
                    If borderFixedControls.Add(lv) Then
                        nativeFixes.Add(New HeaderFillerFix(lv, hHeader))
                    End If
                End If
            End If

        ElseIf TypeOf c Is CheckedListBox OrElse TypeOf c Is ListBox Then
            c.BackColor = UiTheme.BgField
            c.ForeColor = UiTheme.Txt
            If c.IsHandleCreated Then
                UiTheme.ApplyScrollBarTheme(c.Handle)
            End If

        ElseIf TypeOf c Is ToolStrip Then
            ' Copre anche MenuStrip e StatusStrip (derivano da ToolStrip).
            ApplyToolStrip(DirectCast(c, ToolStrip))

        ElseIf TypeOf c Is PictureBox Then
            c.BackColor = UiTheme.BgSidebar

        ElseIf TypeOf c Is TableLayoutPanel Then
            Dim tlp = DirectCast(c, TableLayoutPanel)
            tlp.CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            tlp.BackColor = UiTheme.BgSidebar
            tlp.ForeColor = UiTheme.Txt

        ElseIf TypeOf c Is SplitContainer Then
            Dim sc = DirectCast(c, SplitContainer)
            sc.BorderStyle = BorderStyle.None
            sc.BackColor = UiTheme.BgSidebar
            sc.ForeColor = UiTheme.Txt

        ElseIf TypeOf c Is Panel Then
            Dim pnl = DirectCast(c, Panel)
            pnl.BorderStyle = BorderStyle.None
            pnl.BackColor = UiTheme.BgSidebar
            pnl.ForeColor = UiTheme.Txt

        ElseIf TypeOf c Is TabPage OrElse
               TypeOf c Is UserControl Then
            c.BackColor = UiTheme.BgSidebar
            c.ForeColor = UiTheme.Txt

        Else
            ' Fallback per tipi non riconosciuti (controlli di librerie terze,
            ' es. contenitori custom con BackColor bianco dal designer).
            c.BackColor = UiTheme.BgSidebar
            c.ForeColor = UiTheme.Txt
        End If
    End Sub

    ' Glifo di CheckBox/RadioButton ridipinto a tema sopra il rendering flat.
    Private Sub CheckGlyph_PaintThemed(sender As Object, e As PaintEventArgs)
        Dim bb = TryCast(sender, ButtonBase)
        If bb Is Nothing OrElse bb.Image IsNot Nothing Then Return

        Dim chk = TryCast(sender, CheckBox)
        If chk IsNot Nothing AndAlso chk.Appearance <> Appearance.Normal Then Return
        Dim rdo = TryCast(sender, RadioButton)
        If rdo IsNot Nothing AndAlso rdo.Appearance <> Appearance.Normal Then Return

        Dim g = e.Graphics
        Dim boxSize As Integer = 13
        Dim y As Integer = (bb.Height - boxSize) \ 2
        Dim r As New Rectangle(0, y, boxSize, boxSize)

        ' Il glifo flat di sistema viene disegnato con un piccolo offset
        ' rispetto al nostro: la colonna del glifo va pulita per intero,
        ' altrimenti ne resta visibile un bordo chiaro sfalsato.
        ' (Il testo inizia a x >= 16, quindi non viene toccato.)
        Using bg As New SolidBrush(UiTheme.BgSidebar)
            g.FillRectangle(bg, 0, 0, boxSize + 4, bb.Height)
        End Using

        Dim borderCol As Color = If(bb.Enabled, UiTheme.Border, UiTheme.TxtDim)
        Dim accentCol As Color = If(bb.Enabled, UiTheme.Accent, Color.FromArgb(110, UiTheme.Accent))

        If rdo IsNot Nothing Then
            g.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias
            Using fb As New SolidBrush(UiTheme.BgField)
                g.FillEllipse(fb, r)
            End Using
            Using bp As New Pen(borderCol, 1.0F)
                g.DrawEllipse(bp, r)
            End Using
            If rdo.Checked Then
                Using ab As New SolidBrush(accentCol)
                    g.FillEllipse(ab, r.X + 3, r.Y + 3, boxSize - 6, boxSize - 6)
                End Using
            End If
        Else
            If chk.CheckState = CheckState.Checked Then
                Using ab As New SolidBrush(accentCol)
                    g.FillRectangle(ab, r)
                End Using
                Using cp As New Pen(UiTheme.BgSidebar, 1.8F)
                    g.DrawLine(cp, r.X + 3, r.Y + 7, r.X + 5, r.Y + 9)
                    g.DrawLine(cp, r.X + 5, r.Y + 9, r.X + 10, r.Y + 4)
                End Using
            Else
                Using fb As New SolidBrush(UiTheme.BgField)
                    g.FillRectangle(fb, r)
                End Using
                If chk.CheckState = CheckState.Indeterminate Then
                    Using ab As New SolidBrush(accentCol)
                        g.FillRectangle(ab, r.X + 3, r.Y + 3, boxSize - 6, boxSize - 6)
                    End Using
                End If
            End If
            Using bp As New Pen(borderCol, 1.0F)
                g.DrawRectangle(bp, r)
            End Using
        End If
    End Sub

    ' ----- ToolStrip / MenuStrip / StatusStrip / ContextMenuStrip -----

    Public Sub ApplyToolStrip(ts As ToolStrip)
        If sharedRenderer Is Nothing Then
            sharedRenderer = New UiThemeToolStripRenderer()
        End If

        ts.Renderer = sharedRenderer
        ts.BackColor = UiTheme.BgSidebar
        ts.ForeColor = UiTheme.Txt
        ThemeToolStripItems(ts.Items)
    End Sub

    Private Sub ThemeToolStripItems(items As ToolStripItemCollection)
        For Each it As ToolStripItem In items
            it.ForeColor = UiTheme.Txt

            Dim mi = TryCast(it, ToolStripMenuItem)
            If mi IsNot Nothing AndAlso mi.HasDropDownItems Then
                mi.DropDown.Renderer = sharedRenderer
                mi.DropDown.BackColor = UiTheme.BgField
                ThemeToolStripItems(mi.DropDownItems)
            End If

            Dim cb = TryCast(it, ToolStripComboBox)
            If cb IsNot Nothing Then
                cb.ComboBox.FlatStyle = FlatStyle.Flat
                cb.BackColor = UiTheme.BgField
                cb.ForeColor = UiTheme.Txt
            End If

            Dim tb = TryCast(it, ToolStripTextBox)
            If tb IsNot Nothing Then
                tb.BackColor = UiTheme.BgField
                tb.ForeColor = UiTheme.Txt
                tb.BorderStyle = BorderStyle.FixedSingle
            End If
        Next
    End Sub

    ' ----- GroupBox: bordo e titolo ridisegnati -----

    Private Sub GroupBox_PaintThemed(sender As Object, e As PaintEventArgs)
        Dim gb = DirectCast(sender, GroupBox)
        Dim g = e.Graphics

        g.Clear(gb.BackColor)

        Dim textSize As Size = TextRenderer.MeasureText(gb.Text, gb.Font)
        Dim borderTop As Integer = textSize.Height \ 2

        Using pn As New Pen(UiTheme.Border, 1.0F)
            g.DrawRectangle(pn, 0, borderTop, gb.Width - 1, gb.Height - borderTop - 1)
        End Using

        If gb.Text.Length > 0 Then
            Dim textRect As New Rectangle(8, 0, textSize.Width + 4, textSize.Height)
            Using br As New SolidBrush(gb.BackColor)
                g.FillRectangle(br, textRect)
            End Using
            TextRenderer.DrawText(g, gb.Text, gb.Font,
                                  New Point(10, 0), UiTheme.TxtDim)
        End If
    End Sub

    ' ----- TabControl: linguette owner-draw -----

    Private Sub TabControl_DrawItemThemed(sender As Object, e As DrawItemEventArgs)
        Dim tc = DirectCast(sender, TabControl)
        Dim selected As Boolean = (e.Index = tc.SelectedIndex)
        Dim r As Rectangle = e.Bounds

        Using br As New SolidBrush(If(selected, UiTheme.BgFieldHi, UiTheme.BgSidebar))
            e.Graphics.FillRectangle(br, r)
        End Using

        If selected Then
            Using pn As New Pen(UiTheme.Accent, 2.0F)
                e.Graphics.DrawLine(pn, r.Left + 3, r.Bottom - 2, r.Right - 3, r.Bottom - 2)
            End Using
        End If

        TextRenderer.DrawText(e.Graphics, tc.TabPages(e.Index).Text, tc.Font, r,
                              If(selected, UiTheme.Txt, UiTheme.TxtDim),
                              TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter)
    End Sub

    ' ----- Bordo non-client a tema per TextBox (il FixedSingle di sistema
    ' ----- resta grigio): ridipinto dopo NCPAINT/PAINT. -----

    Private Class NcBorderFix
        Inherits NativeWindow

        Private ReadOnly ctl As Control

        Public Sub New(c As Control)
            ctl = c
            If c.IsHandleCreated Then AssignHandle(c.Handle)
            AddHandler c.HandleCreated, Sub(sender As Object, e As EventArgs) AssignHandle(ctl.Handle)
            AddHandler c.HandleDestroyed, Sub(sender As Object, e As EventArgs) ReleaseHandle()
        End Sub

        Protected Overrides Sub WndProc(ByRef m As Message)
            MyBase.WndProc(m)

            ' WM_NCPAINT / WM_PAINT / WM_KILLFOCUS / WM_SETFOCUS
            If m.Msg = &H85 OrElse m.Msg = &HF OrElse m.Msg = &H8 OrElse m.Msg = &H7 Then
                DrawBorder()
            End If
        End Sub

        Private Sub DrawBorder()
            If Handle = IntPtr.Zero Then Return

            Dim hdc As IntPtr = GetWindowDC(Handle)
            If hdc = IntPtr.Zero Then Return

            Try
                Using g As Graphics = Graphics.FromHdc(hdc)
                    Using pn As New Pen(UiTheme.Border, 1.0F)
                        g.DrawRectangle(pn, 0, 0, ctl.Width - 1, ctl.Height - 1)
                    End Using
                End Using
            Finally
                ReleaseDC(Handle, hdc)
            End Try
        End Sub
    End Class

    ' ----- Copre a tema il riempimento dell'header ListView oltre
    ' ----- l'ultima colonna (dipinto bianco dal controllo di sistema). -----

    Private Class HeaderFillerFix
        Inherits NativeWindow

        Private ReadOnly lv As ListView

        Public Sub New(owner As ListView, hHeader As IntPtr)
            lv = owner
            AssignHandle(hHeader)
            AddHandler owner.HandleDestroyed, Sub(sender As Object, e As EventArgs) ReleaseHandle()
        End Sub

        Protected Overrides Sub WndProc(ByRef m As Message)
            MyBase.WndProc(m)

            If m.Msg = &HF Then   ' WM_PAINT
                PaintFiller()
            End If
        End Sub

        Private Sub PaintFiller()
            If Handle = IntPtr.Zero OrElse lv Is Nothing OrElse lv.IsDisposed Then Return

            Dim total As Integer = 0
            For Each col As ColumnHeader In lv.Columns
                total += col.Width
            Next

            Const SB_HORZ As Integer = 0
            Dim scrollX As Integer = GetScrollPos(lv.Handle, SB_HORZ)
            Dim fillerLeft As Integer = total - scrollX

            Using g As Graphics = Graphics.FromHwnd(Handle)
                Dim w As Integer = CInt(g.VisibleClipBounds.Width)
                Dim h As Integer = CInt(g.VisibleClipBounds.Height)

                If fillerLeft < w Then
                    Using br As New SolidBrush(UiTheme.BgSidebar)
                        g.FillRectangle(br, fillerLeft, 0, w - fillerLeft, h)
                    End Using
                    Using pn As New Pen(UiTheme.Border, 1.0F)
                        g.DrawLine(pn, fillerLeft, h - 1, w, h - 1)
                    End Using
                End If
            End Using
        End Sub
    End Class

    ' ----- Copre la cornice 3D del TabControl attorno alla pagina attiva
    ' ----- (dipinta con colori di sistema, non ricolorabile). -----

    Private Class TabFrameFix
        Inherits NativeWindow

        Private ReadOnly tc As TabControl

        Public Sub New(target As TabControl)
            tc = target
            If target.IsHandleCreated Then AssignHandle(target.Handle)
            AddHandler target.HandleCreated, Sub(sender As Object, e As EventArgs) AssignHandle(tc.Handle)
            AddHandler target.HandleDestroyed, Sub(sender As Object, e As EventArgs) ReleaseHandle()
        End Sub

        Protected Overrides Sub WndProc(ByRef m As Message)
            MyBase.WndProc(m)

            If m.Msg = &HF Then   ' WM_PAINT: ridipingo i margini dopo il default
                PaintFrame()
            End If
        End Sub

        Private Sub PaintFrame()
            If Handle = IntPtr.Zero OrElse tc.TabCount = 0 Then Return

            Dim disp As Rectangle = tc.DisplayRectangle
            Dim client As Rectangle = tc.ClientRectangle

            Dim tabsBottom As Integer = 0
            Try
                tabsBottom = tc.GetTabRect(0).Bottom
            Catch
                tabsBottom = disp.Top - 4
            End Try

            Using g As Graphics = Graphics.FromHwnd(Handle)
                Using br As New SolidBrush(UiTheme.BgSidebar)
                    ' Fascia delle linguette: il controllo la dipinge con colori
                    ' di sistema (sfondo e bordi delle linguette): coperta per
                    ' intero e linguette ridisegnate a tema qui sotto.
                    g.FillRectangle(br, client.Left, client.Top, client.Width, tabsBottom - client.Top)

                    ' banda sopra la pagina (tra linguette e contenuto)
                    g.FillRectangle(br, client.Left, tabsBottom, client.Width, disp.Top - tabsBottom)
                    ' bordo sinistro / destro / inferiore
                    g.FillRectangle(br, client.Left, disp.Top, disp.Left - client.Left, client.Bottom - disp.Top)
                    g.FillRectangle(br, disp.Right, disp.Top, client.Right - disp.Right, client.Bottom - disp.Top)
                    g.FillRectangle(br, client.Left, disp.Bottom, client.Width, client.Bottom - disp.Bottom)
                End Using

                ' Linguette a tema (selezionata: campo chiaro + sottolineatura accento).
                For i As Integer = 0 To tc.TabCount - 1
                    Dim r As Rectangle = tc.GetTabRect(i)
                    Dim isSel As Boolean = (i = tc.SelectedIndex)

                    If isSel Then
                        Using sb As New SolidBrush(UiTheme.BgFieldHi)
                            g.FillRectangle(sb, r)
                        End Using
                        Using ap As New Pen(UiTheme.Accent, 2.0F)
                            g.DrawLine(ap, r.Left + 3, r.Bottom - 2, r.Right - 3, r.Bottom - 2)
                        End Using
                    End If

                    TextRenderer.DrawText(g, tc.TabPages(i).Text, tc.Font, r,
                                          If(isSel, UiTheme.Txt, UiTheme.TxtDim),
                                          TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter)
                Next

                ' filo di separazione sotto la riga delle linguette
                Using pn As New Pen(UiTheme.Border, 1.0F)
                    g.DrawLine(pn, client.Left, tabsBottom, client.Right, tabsBottom)
                End Using
            End Using
        End Sub
    End Class

End Module

' ============================================================
'  Renderer professionale per ToolStrip/menu con la palette del tema.
' ============================================================
Public Class UiThemeToolStripRenderer
    Inherits ToolStripProfessionalRenderer

    Public Sub New()
        MyBase.New(New UiThemeColorTable())
        RoundedEdges = False
    End Sub

    Protected Overrides Sub OnRenderArrow(e As ToolStripArrowRenderEventArgs)
        e.ArrowColor = UiTheme.Txt
        MyBase.OnRenderArrow(e)
    End Sub
End Class

Public Class UiThemeColorTable
    Inherits ProfessionalColorTable

    ' Sfondo barre
    Public Overrides ReadOnly Property ToolStripGradientBegin As Color
        Get
            Return UiTheme.BgSidebar
        End Get
    End Property

    Public Overrides ReadOnly Property ToolStripGradientMiddle As Color
        Get
            Return UiTheme.BgSidebar
        End Get
    End Property

    Public Overrides ReadOnly Property ToolStripGradientEnd As Color
        Get
            Return UiTheme.BgSidebar
        End Get
    End Property

    Public Overrides ReadOnly Property ToolStripBorder As Color
        Get
            Return UiTheme.Border
        End Get
    End Property

    Public Overrides ReadOnly Property MenuStripGradientBegin As Color
        Get
            Return UiTheme.BgSidebar
        End Get
    End Property

    Public Overrides ReadOnly Property MenuStripGradientEnd As Color
        Get
            Return UiTheme.BgSidebar
        End Get
    End Property

    Public Overrides ReadOnly Property StatusStripGradientBegin As Color
        Get
            Return UiTheme.StatusBg
        End Get
    End Property

    Public Overrides ReadOnly Property StatusStripGradientEnd As Color
        Get
            Return UiTheme.StatusBg
        End Get
    End Property

    ' Tendine
    Public Overrides ReadOnly Property ToolStripDropDownBackground As Color
        Get
            Return UiTheme.BgField
        End Get
    End Property

    Public Overrides ReadOnly Property ImageMarginGradientBegin As Color
        Get
            Return UiTheme.BgField
        End Get
    End Property

    Public Overrides ReadOnly Property ImageMarginGradientMiddle As Color
        Get
            Return UiTheme.BgField
        End Get
    End Property

    Public Overrides ReadOnly Property ImageMarginGradientEnd As Color
        Get
            Return UiTheme.BgField
        End Get
    End Property

    Public Overrides ReadOnly Property MenuBorder As Color
        Get
            Return UiTheme.Border
        End Get
    End Property

    ' Voci di menu
    Public Overrides ReadOnly Property MenuItemSelected As Color
        Get
            Return UiTheme.BgFieldHi
        End Get
    End Property

    Public Overrides ReadOnly Property MenuItemSelectedGradientBegin As Color
        Get
            Return UiTheme.BgFieldHi
        End Get
    End Property

    Public Overrides ReadOnly Property MenuItemSelectedGradientEnd As Color
        Get
            Return UiTheme.BgFieldHi
        End Get
    End Property

    Public Overrides ReadOnly Property MenuItemBorder As Color
        Get
            Return UiTheme.Border
        End Get
    End Property

    Public Overrides ReadOnly Property MenuItemPressedGradientBegin As Color
        Get
            Return UiTheme.BgField
        End Get
    End Property

    Public Overrides ReadOnly Property MenuItemPressedGradientEnd As Color
        Get
            Return UiTheme.BgField
        End Get
    End Property

    ' Pulsanti toolbar (hover / premuto / checked)
    Public Overrides ReadOnly Property ButtonSelectedHighlight As Color
        Get
            Return UiTheme.BgFieldHi
        End Get
    End Property

    Public Overrides ReadOnly Property ButtonSelectedGradientBegin As Color
        Get
            Return UiTheme.BgFieldHi
        End Get
    End Property

    Public Overrides ReadOnly Property ButtonSelectedGradientEnd As Color
        Get
            Return UiTheme.BgFieldHi
        End Get
    End Property

    Public Overrides ReadOnly Property ButtonSelectedBorder As Color
        Get
            Return UiTheme.Border
        End Get
    End Property

    Public Overrides ReadOnly Property ButtonPressedGradientBegin As Color
        Get
            Return UiTheme.Border
        End Get
    End Property

    Public Overrides ReadOnly Property ButtonPressedGradientEnd As Color
        Get
            Return UiTheme.Border
        End Get
    End Property

    Public Overrides ReadOnly Property ButtonPressedBorder As Color
        Get
            Return UiTheme.Accent
        End Get
    End Property

    Public Overrides ReadOnly Property ButtonCheckedGradientBegin As Color
        Get
            Return Color.FromArgb(70, UiTheme.Accent)
        End Get
    End Property

    Public Overrides ReadOnly Property ButtonCheckedGradientEnd As Color
        Get
            Return Color.FromArgb(70, UiTheme.Accent)
        End Get
    End Property

    Public Overrides ReadOnly Property CheckBackground As Color
        Get
            Return Color.FromArgb(90, UiTheme.Accent)
        End Get
    End Property

    Public Overrides ReadOnly Property CheckSelectedBackground As Color
        Get
            Return Color.FromArgb(140, UiTheme.Accent)
        End Get
    End Property

    Public Overrides ReadOnly Property CheckPressedBackground As Color
        Get
            Return Color.FromArgb(180, UiTheme.Accent)
        End Get
    End Property

    ' Separatori e grip
    Public Overrides ReadOnly Property SeparatorDark As Color
        Get
            Return UiTheme.Border
        End Get
    End Property

    Public Overrides ReadOnly Property SeparatorLight As Color
        Get
            Return UiTheme.BgSidebar
        End Get
    End Property

    Public Overrides ReadOnly Property GripDark As Color
        Get
            Return UiTheme.Border
        End Get
    End Property

    Public Overrides ReadOnly Property GripLight As Color
        Get
            Return UiTheme.BgSidebar
        End Get
    End Property

    Public Overrides ReadOnly Property OverflowButtonGradientBegin As Color
        Get
            Return UiTheme.BgSidebar
        End Get
    End Property

    Public Overrides ReadOnly Property OverflowButtonGradientMiddle As Color
        Get
            Return UiTheme.BgSidebar
        End Get
    End Property

    Public Overrides ReadOnly Property OverflowButtonGradientEnd As Color
        Get
            Return UiTheme.BgSidebar
        End Get
    End Property
End Class