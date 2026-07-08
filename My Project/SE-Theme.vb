' ============================================================
'  SE-Theme.vb - Tema scuro/chiaro riusabile (da SE-Voronoi)
'
'  Contenuto autonomo, senza dipendenze dal resto dell'app:
'    UiTheme              palette commutabile (SetTheme), titlebar DWM,
'                         scrollbar native a tema, RoundedRect
'    ThemedButton         pulsante arrotondato (colori via proprieta'/FlatAppearance)
'    ThemedCheckBox       checkbox custom
'    ThemedNumericUpDown  campo numerico con frecce
'    ThemedComboBox       combo con tendina owner-drawn (PerformWheel)
'    ThemedSlider         slider con valore (PerformWheel)
'    ThemedVScrollBar     scrollbar verticale sottile (evento ScrollChanged)
'    CollapsibleSection   sezione a scomparsa (RefreshTheme)
'    WheelToScrollFilter  rotella -> scrollbar per aree custom
'
'  Uso minimo:
'    UiTheme.SetTheme(True)                 ' dark (False = light)
'    UiTheme.ApplyTitleBarTheme(Handle)     ' in OnLoad del form
'    i controlli leggono la palette UiTheme a ogni paint
'  Pulsanti: BackColor=UiTheme.BgField (o Accent per il primario),
'  FlatAppearance.BorderColor / MouseOverBackColor / MouseDownBackColor.
' ============================================================

Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Windows.Forms

Public NotInheritable Class UiTheme
    Private Sub New()
    End Sub

    Public Shared IsDark As Boolean = True

    ' Il canvas resta navy in entrambi i temi: e' la superficie di disegno
    ' e la palette delle celle e' tarata su di esso.
    Public Shared BgCanvas As Color = Color.FromArgb(8, 6, 53)
    Public Shared BgSidebar As Color = Color.FromArgb(16, 14, 60)
    Public Shared BgField As Color = Color.FromArgb(22, 20, 74)
    Public Shared BgFieldHi As Color = Color.FromArgb(30, 28, 94)
    Public Shared Border As Color = Color.FromArgb(42, 40, 112)
    Public Shared Txt As Color = Color.FromArgb(232, 236, 248)
    Public Shared TxtDim As Color = Color.FromArgb(154, 160, 200)
    Public Shared Accent As Color = Color.FromArgb(0, 188, 212)
    Public Shared AccentHi As Color = Color.FromArgb(110, 231, 243)
    Public Shared StatusBg As Color = Color.FromArgb(13, 11, 69)

    Public Shared Sub SetTheme(dark As Boolean)
        IsDark = dark

        If dark Then
            BgSidebar = Color.FromArgb(16, 14, 60)
            BgField = Color.FromArgb(22, 20, 74)
            BgFieldHi = Color.FromArgb(30, 28, 94)
            Border = Color.FromArgb(42, 40, 112)
            Txt = Color.FromArgb(232, 236, 248)
            TxtDim = Color.FromArgb(154, 160, 200)
            Accent = Color.FromArgb(0, 188, 212)
            AccentHi = Color.FromArgb(110, 231, 243)
            StatusBg = Color.FromArgb(13, 11, 69)
        Else
            BgSidebar = Color.FromArgb(238, 241, 246)
            BgField = Color.FromArgb(255, 255, 255)
            BgFieldHi = Color.FromArgb(222, 230, 240)
            Border = Color.FromArgb(178, 186, 206)
            Txt = Color.FromArgb(30, 40, 55)
            TxtDim = Color.FromArgb(106, 116, 140)
            Accent = Color.FromArgb(0, 151, 167)
            AccentHi = Color.FromArgb(0, 188, 212)
            StatusBg = Color.FromArgb(226, 230, 238)
        End If
    End Sub

    <Runtime.InteropServices.DllImport("dwmapi.dll", EntryPoint:="DwmSetWindowAttribute")>
    Private Shared Function DwmSetAttr(hwnd As IntPtr, attr As Integer, ByRef attrValue As Integer, attrSize As Integer) As Integer
    End Function

    <Runtime.InteropServices.DllImport("user32.dll")>
    Private Shared Function SetWindowPos(hWnd As IntPtr, hWndAfter As IntPtr, x As Integer, y As Integer, cx As Integer, cy As Integer, flags As UInteger) As Boolean
    End Function

    <Runtime.InteropServices.DllImport("uxtheme.dll", CharSet:=Runtime.InteropServices.CharSet.Unicode)>
    Private Shared Function SetWindowTheme(hWnd As IntPtr, pszSubAppName As String, pszSubIdList As String) As Integer
    End Function

    ' Scrollbar native (RichTextBox, AutoScroll) scure o chiare secondo il tema.
    Public Shared Sub ApplyScrollBarTheme(handle As IntPtr)
        Try
            SetWindowTheme(handle, If(IsDark, "DarkMode_Explorer", "Explorer"), Nothing)
        Catch
        End Try
    End Sub

    ' Applica alla barra del titolo i colori del tema corrente (Win10 1809+/11).
    Public Shared Sub ApplyTitleBarTheme(handle As IntPtr)
        Try
            Dim dark As Integer = If(IsDark, 1, 0)
            If DwmSetAttr(handle, 20, dark, 4) <> 0 Then
                DwmSetAttr(handle, 19, dark, 4)
            End If
            Dim captionCol As Integer = BgSidebar.R Or (CInt(BgSidebar.G) << 8) Or (CInt(BgSidebar.B) << 16)
            Dim textCol As Integer = Txt.R Or (CInt(Txt.G) << 8) Or (CInt(Txt.B) << 16)
            DwmSetAttr(handle, 35, captionCol, 4)
            DwmSetAttr(handle, 34, captionCol, 4)
            DwmSetAttr(handle, 36, textCol, 4)

            ' Forza il ridisegno dell'area non-client: senza, Windows a volte
            ' aggiorna la caption solo quando la finestra perde/riprende il fuoco.
            ' SWP_NOSIZE Or SWP_NOMOVE Or SWP_NOZORDER Or SWP_NOACTIVATE Or SWP_FRAMECHANGED
            SetWindowPos(handle, IntPtr.Zero, 0, 0, 0, 0, &H1 Or &H2 Or &H4 Or &H10 Or &H20)
        Catch
        End Try
    End Sub

    Public Shared Function RoundedRect(r As RectangleF, radius As Single) As Drawing2D.GraphicsPath
        Dim gp As New Drawing2D.GraphicsPath()
        Dim d As Single = radius * 2.0F
        If d <= 0.0F OrElse r.Width <= d OrElse r.Height <= d Then
            gp.AddRectangle(r)
            Return gp
        End If
        gp.AddArc(r.X, r.Y, d, d, 180, 90)
        gp.AddArc(r.Right - d, r.Y, d, d, 270, 90)
        gp.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90)
        gp.AddArc(r.X, r.Bottom - d, d, d, 90, 90)
        gp.CloseFigure()
        Return gp
    End Function
End Class

' ============================================================
'  Inoltra la rotella del mouse a una ThemedVScrollBar quando il cursore
'  e' sopra l'area indicata e la sua finestra e' attiva.
' ============================================================
Public Class WheelToScrollFilter
    Implements IMessageFilter

    Private ReadOnly area As Control
    Private ReadOnly bar As ThemedVScrollBar

    Public Sub New(scrollArea As Control, scrollBar As ThemedVScrollBar)
        area = scrollArea
        bar = scrollBar
    End Sub

    Public Function PreFilterMessage(ByRef m As Message) As Boolean Implements IMessageFilter.PreFilterMessage
        Const WM_MOUSEWHEEL As Integer = &H20A
        If m.Msg <> WM_MOUSEWHEEL Then Return False
        If area Is Nothing OrElse Not area.IsHandleCreated OrElse Not area.Visible Then Return False

        Dim owner As Form = area.FindForm()
        If owner Is Nothing OrElse Form.ActiveForm IsNot owner Then Return False

        Dim pos As Point = Control.MousePosition
        If Not area.RectangleToScreen(area.ClientRectangle).Contains(pos) Then Return False

        If bar IsNot Nothing AndAlso bar.Visible Then
            Dim raw As Integer = CInt((m.WParam.ToInt64() >> 16) And &HFFFF&)
            If raw >= &H8000 Then raw -= &H10000
            bar.Value -= Math.Sign(raw) * 60
        End If
        Return True
    End Function
End Class

' ============================================================
'  Slider a tema con etichetta del valore. Espone la stessa interfaccia
'  di NumericUpDown (Minimum/Maximum/Value/Increment/DecimalPlaces,
'  evento ValueChanged) cosi' puo' sostituirlo senza toccare i chiamanti.
' ============================================================
Public Class ThemedSlider
    Inherits Control

    Private _min As Decimal = 0D
    Private _max As Decimal = 100D
    Private _val As Decimal = 0D
    Private _inc As Decimal = 1D
    Private _decimals As Integer = 0
    Private dragging As Boolean = False
    Private hovering As Boolean = False

    Private Const ValueTextW As Integer = 34
    Private Const PadX As Integer = 8

    Public Event ValueChanged As EventHandler

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or
                 ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.OptimizedDoubleBuffer Or
                 ControlStyles.ResizeRedraw Or
                 ControlStyles.Selectable, True)
        Height = 26
        Cursor = Cursors.Hand
        BackColor = UiTheme.BgSidebar
        ForeColor = UiTheme.Txt
    End Sub

    Public Property Minimum As Decimal
        Get
            Return _min
        End Get
        Set(value As Decimal)
            _min = value
            If _val < _min Then Me.Value = _min
            Invalidate()
        End Set
    End Property

    Public Property Maximum As Decimal
        Get
            Return _max
        End Get
        Set(value As Decimal)
            _max = value
            If _val > _max Then Me.Value = _max
            Invalidate()
        End Set
    End Property

    Public Property Increment As Decimal
        Get
            Return _inc
        End Get
        Set(value As Decimal)
            If value > 0D Then _inc = value
        End Set
    End Property

    Public Property DecimalPlaces As Integer
        Get
            Return _decimals
        End Get
        Set(value As Integer)
            _decimals = Math.Max(0, value)
            Invalidate()
        End Set
    End Property

    Public Property Value As Decimal
        Get
            Return _val
        End Get
        Set(value As Decimal)
            Dim v As Decimal = value
            If v < _min Then v = _min
            If v > _max Then v = _max
            If v <> _val Then
                _val = v
                Invalidate()
                RaiseEvent ValueChanged(Me, EventArgs.Empty)
            End If
        End Set
    End Property

    Private Sub SetValueFromX(x As Integer)
        Dim trackL As Integer = PadX
        Dim trackR As Integer = Width - ValueTextW - 2
        If trackR <= trackL Then Return

        Dim t As Double = (x - trackL) / CDbl(trackR - trackL)
        If t < 0.0 Then t = 0.0
        If t > 1.0 Then t = 1.0

        Dim raw As Decimal = _min + CDec(t) * (_max - _min)
        Dim snapped As Decimal = Math.Round(raw / _inc) * _inc
        snapped = Math.Round(snapped, Math.Max(_decimals, 4))
        Me.Value = snapped
    End Sub

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        If Not Enabled Then Return
        Focus()
        dragging = True
        SetValueFromX(e.X)
    End Sub

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        If dragging Then SetValueFromX(e.X)
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        dragging = False
    End Sub

    Protected Overrides Sub OnMouseWheel(e As MouseEventArgs)
        MyBase.OnMouseWheel(e)
        PerformWheel(e.Delta)
    End Sub

    ' Rotella inoltrata dal message filter quando il cursore e' sopra il controllo.
    Public Sub PerformWheel(delta As Integer)
        If Not Enabled Then Return
        Me.Value = _val + If(delta > 0, _inc, -_inc)
    End Sub

    Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
        MyBase.OnKeyDown(e)
        If Not Enabled Then Return
        If e.KeyCode = Keys.Left OrElse e.KeyCode = Keys.Down Then
            Me.Value = _val - _inc
        ElseIf e.KeyCode = Keys.Right OrElse e.KeyCode = Keys.Up Then
            Me.Value = _val + _inc
        End If
    End Sub

    Protected Overrides Sub OnMouseEnter(e As EventArgs)
        MyBase.OnMouseEnter(e)
        hovering = True
        Invalidate()
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        MyBase.OnMouseLeave(e)
        hovering = False
        Invalidate()
    End Sub

    Protected Overrides Sub OnEnabledChanged(e As EventArgs)
        MyBase.OnEnabledChanged(e)
        Invalidate()
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        e.Graphics.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias
        e.Graphics.PixelOffsetMode = Drawing2D.PixelOffsetMode.HighQuality
        e.Graphics.Clear(BackColor)

        Dim trackL As Integer = PadX
        Dim trackR As Integer = Width - ValueTextW - 2
        Dim cy As Single = Height / 2.0F

        Dim range As Decimal = _max - _min
        Dim t As Single = 0.0F
        If range > 0D Then t = CSng((_val - _min) / range)

        Dim thumbX As Single = trackL + (trackR - trackL) * t

        Dim trackCol As Color = If(Enabled, UiTheme.Border, Color.FromArgb(34, 32, 90))
        Dim fillCol As Color = If(Enabled, UiTheme.Accent, Color.FromArgb(0, 100, 115))
        Dim thumbCol As Color = If(Not Enabled, Color.FromArgb(0, 110, 125),
                                   If(hovering OrElse dragging, UiTheme.AccentHi, UiTheme.Accent))

        Using p As New Pen(trackCol, 3.0F)
            p.StartCap = Drawing2D.LineCap.Round
            p.EndCap = Drawing2D.LineCap.Round
            e.Graphics.DrawLine(p, trackL, cy, trackR, cy)
        End Using

        If thumbX > trackL Then
            Using p As New Pen(fillCol, 3.0F)
                p.StartCap = Drawing2D.LineCap.Round
                p.EndCap = Drawing2D.LineCap.Round
                e.Graphics.DrawLine(p, trackL, cy, thumbX, cy)
            End Using
        End If

        Dim r As Single = If(hovering OrElse dragging, 6.5F, 5.5F)
        Using b As New SolidBrush(thumbCol)
            e.Graphics.FillEllipse(b, thumbX - r, cy - r, r * 2.0F, r * 2.0F)
        End Using
        Using p As New Pen(BackColor, 1.5F)
            e.Graphics.DrawEllipse(p, thumbX - r, cy - r, r * 2.0F, r * 2.0F)
        End Using

        Dim txt As String = _val.ToString("F" & _decimals, Globalization.CultureInfo.CurrentCulture)
        Using b As New SolidBrush(If(Enabled, UiTheme.Txt, UiTheme.TxtDim))
            Dim sz = e.Graphics.MeasureString(txt, Font)
            e.Graphics.DrawString(txt, Font, b, Width - sz.Width - 2, cy - sz.Height / 2.0F)
        End Using
    End Sub
End Class

' ============================================================
'  Sezione collassabile per la sidebar: header cliccabile (chevron +
'  titolo) e contenuto TableLayoutPanel a colonna singola, compatibile
'  con gli helper AddRowTitle/AddRowControl/AddDoubleRow.
' ============================================================
Public Class CollapsibleSection
    Inherits TableLayoutPanel

    Private ReadOnly headerLbl As New Label()
    Public ReadOnly Content As New TableLayoutPanel()
    Private ReadOnly titleText As String
    Private isOpen As Boolean = True

    ' Stato esposto per la persistenza (apri/chiudi tra sessioni).
    Public ReadOnly Property SectionTitle As String
        Get
            Return titleText
        End Get
    End Property

    Public ReadOnly Property Expanded As Boolean
        Get
            Return isOpen
        End Get
    End Property

    Public Sub New(title As String, Optional startOpen As Boolean = True)
        titleText = title
        isOpen = startOpen

        ColumnCount = 1
        RowCount = 2
        RowStyles.Add(New RowStyle(SizeType.AutoSize))
        RowStyles.Add(New RowStyle(SizeType.AutoSize))
        AutoSize = True
        AutoSizeMode = AutoSizeMode.GrowAndShrink
        Margin = New Padding(0, 2, 0, 2)
        BackColor = UiTheme.BgSidebar
        Width = 244

        headerLbl.AutoSize = False
        headerLbl.Width = 244
        headerLbl.Height = 26
        headerLbl.Margin = New Padding(0, 2, 0, 0)
        headerLbl.TextAlign = ContentAlignment.BottomLeft
        headerLbl.Font = New Font("Segoe UI", 8.0F, FontStyle.Bold)
        headerLbl.Cursor = Cursors.Hand
        headerLbl.BackColor = UiTheme.BgSidebar
        AddHandler headerLbl.Click, AddressOf Header_Click
        AddHandler headerLbl.Paint, AddressOf Header_Paint

        Content.ColumnCount = 1
        Content.RowCount = 0
        Content.AutoSize = True
        Content.AutoSizeMode = AutoSizeMode.GrowAndShrink
        Content.GrowStyle = TableLayoutPanelGrowStyle.AddRows
        Content.Margin = New Padding(0)
        Content.Padding = New Padding(0)
        Content.BackColor = UiTheme.BgSidebar
        Content.Width = 244

        Controls.Add(headerLbl, 0, 0)
        Controls.Add(Content, 0, 1)

        Content.Visible = isOpen
        UpdateHeader()
    End Sub

    Private Sub Header_Click(sender As Object, e As EventArgs)
        isOpen = Not isOpen
        Content.Visible = isOpen
        UpdateHeader()
    End Sub

    Private Sub UpdateHeader()
        Dim chevron As String = If(isOpen, ChrW(&H25BC), ChrW(&H25B6))
        headerLbl.Text = chevron & "  " & titleText.ToUpperInvariant()
        headerLbl.ForeColor = If(isOpen, UiTheme.Accent, UiTheme.TxtDim)
    End Sub

    Private Sub Header_Paint(sender As Object, e As PaintEventArgs)
        ' Sottile linea separatrice sopra l'header.
        Using p As New Pen(UiTheme.Border, 1.0F)
            e.Graphics.DrawLine(p, 0, 0, headerLbl.Width, 0)
        End Using
    End Sub

    ' Riapplica i colori del tema corrente (usato dal toggle dark/light).
    Public Sub RefreshTheme()
        BackColor = UiTheme.BgSidebar
        headerLbl.BackColor = UiTheme.BgSidebar
        Content.BackColor = UiTheme.BgSidebar
        UpdateHeader()
        Invalidate(True)
    End Sub
End Class

' ============================================================
'  Pulsante a tema con angoli arrotondati e stati hover/pressed.
'  I colori arrivano da StyleButton (BackColor/ForeColor/FlatAppearance),
'  quindi l'interfaccia resta quella di Button.
' ============================================================
Public Class ThemedButton
    Inherits Button

    Private hovering As Boolean = False
    Private pressed As Boolean = False

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or
                 ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.OptimizedDoubleBuffer, True)
    End Sub

    Protected Overrides Sub OnMouseEnter(e As EventArgs)
        MyBase.OnMouseEnter(e)
        hovering = True
        Invalidate()
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        MyBase.OnMouseLeave(e)
        hovering = False
        pressed = False
        Invalidate()
    End Sub

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        pressed = True
        Invalidate()
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        pressed = False
        Invalidate()
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        e.Graphics.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias
        e.Graphics.PixelOffsetMode = Drawing2D.PixelOffsetMode.HighQuality

        ' Sfondo del contenitore dietro gli angoli arrotondati.
        Dim parentBg As Color = If(Parent IsNot Nothing, Parent.BackColor, UiTheme.BgSidebar)
        e.Graphics.Clear(parentBg)

        Dim bg As Color = BackColor
        If Not Enabled Then
            bg = Color.FromArgb(Math.Max(0, bg.R \ 2), Math.Max(0, bg.G \ 2 + 10), Math.Max(0, bg.B \ 2 + 20))
        ElseIf pressed AndAlso Not FlatAppearance.MouseDownBackColor.IsEmpty Then
            bg = FlatAppearance.MouseDownBackColor
        ElseIf hovering AndAlso Not FlatAppearance.MouseOverBackColor.IsEmpty Then
            bg = FlatAppearance.MouseOverBackColor
        End If

        Dim r As New RectangleF(0.5F, 0.5F, Width - 1.0F, Height - 1.0F)
        Using gp = UiTheme.RoundedRect(r, 6.0F)
            Using b As New SolidBrush(bg)
                e.Graphics.FillPath(b, gp)
            End Using
            If FlatAppearance.BorderSize > 0 Then
                Using p As New Pen(If(hovering AndAlso Enabled, UiTheme.Accent, FlatAppearance.BorderColor), 1.0F)
                    e.Graphics.DrawPath(p, gp)
                End Using
            End If
        End Using

        Dim txtCol As Color = If(Enabled, ForeColor, UiTheme.TxtDim)
        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, txtCol,
                              TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter)
    End Sub
End Class

' ============================================================
'  CheckBox a tema: casella scura arrotondata, spunta su fondo accento.
'  Eredita da CheckBox: toggle, eventi e Checked restano quelli standard.
' ============================================================
Public Class ThemedCheckBox
    Inherits CheckBox

    Private hovering As Boolean = False

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or
                 ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.OptimizedDoubleBuffer, True)
        Cursor = Cursors.Hand
    End Sub

    Protected Overrides Sub OnMouseEnter(e As EventArgs)
        MyBase.OnMouseEnter(e)
        hovering = True
        Invalidate()
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        MyBase.OnMouseLeave(e)
        hovering = False
        Invalidate()
    End Sub

    Protected Overrides Sub OnCheckedChanged(e As EventArgs)
        MyBase.OnCheckedChanged(e)
        Invalidate()
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        e.Graphics.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias
        e.Graphics.PixelOffsetMode = Drawing2D.PixelOffsetMode.HighQuality
        e.Graphics.Clear(BackColor)

        Dim boxSize As Single = 15.0F
        Dim by As Single = (Height - boxSize) / 2.0F
        Dim box As New RectangleF(1.0F, by, boxSize, boxSize)

        Using gp = UiTheme.RoundedRect(box, 3.0F)
            If Checked Then
                Dim fill As Color = If(Not Enabled, Color.FromArgb(0, 110, 125),
                                       If(hovering, UiTheme.AccentHi, UiTheme.Accent))
                Using b As New SolidBrush(fill)
                    e.Graphics.FillPath(b, gp)
                End Using
            Else
                Using b As New SolidBrush(If(hovering AndAlso Enabled, UiTheme.BgFieldHi, UiTheme.BgField))
                    e.Graphics.FillPath(b, gp)
                End Using
                Using p As New Pen(If(hovering AndAlso Enabled, UiTheme.Accent, UiTheme.Border), 1.0F)
                    e.Graphics.DrawPath(p, gp)
                End Using
            End If
        End Using

        If Checked Then
            Using p As New Pen(UiTheme.BgCanvas, 2.0F)
                p.StartCap = Drawing2D.LineCap.Round
                p.EndCap = Drawing2D.LineCap.Round
                p.LineJoin = Drawing2D.LineJoin.Round
                e.Graphics.DrawLines(p, New PointF() {
                    New PointF(box.X + 3.5F, box.Y + 8.0F),
                    New PointF(box.X + 6.5F, box.Y + 11.0F),
                    New PointF(box.X + 11.5F, box.Y + 4.5F)
                })
            End Using
        End If

        Dim txtCol As Color = If(Enabled, ForeColor, UiTheme.TxtDim)
        Dim txtRect As New Rectangle(CInt(boxSize) + 7, 0, Width - CInt(boxSize) - 7, Height)
        TextRenderer.DrawText(e.Graphics, Text, Font, txtRect, txtCol,
                              TextFormatFlags.Left Or TextFormatFlags.VerticalCenter)
    End Sub
End Class

' ============================================================
'  NumericUpDown a tema: campo di testo scuro + frecce disegnate.
'  Espone la stessa interfaccia usata dal resto del codice
'  (Minimum/Maximum/Value/Increment/DecimalPlaces, ValueChanged).
' ============================================================
Public Class ThemedNumericUpDown
    Inherits Control

    Private ReadOnly box As New TextBox()
    Private _min As Decimal = 0D
    Private _max As Decimal = 100D
    Private _val As Decimal = 0D
    Private _inc As Decimal = 1D
    Private _decimals As Integer = 0
    Private updatingText As Boolean = False
    Private hoverZone As Integer = 0    ' 0 nessuna, 1 su, 2 giu

    Private Const ArrowW As Integer = 18

    Public Event ValueChanged As EventHandler

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or
                 ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.OptimizedDoubleBuffer Or
                 ControlStyles.ResizeRedraw, True)
        Height = 26
        BackColor = UiTheme.BgField

        box.BorderStyle = BorderStyle.None
        box.BackColor = UiTheme.BgField
        box.ForeColor = UiTheme.Txt
        box.TextAlign = HorizontalAlignment.Left
        AddHandler box.KeyDown, AddressOf Box_KeyDown
        AddHandler box.Leave, AddressOf Box_Leave
        Controls.Add(box)

        LayoutBox()
        SyncText()
    End Sub

    Public Property Minimum As Decimal
        Get
            Return _min
        End Get
        Set(value As Decimal)
            _min = value
            If _val < _min Then Me.Value = _min
        End Set
    End Property

    Public Property Maximum As Decimal
        Get
            Return _max
        End Get
        Set(value As Decimal)
            _max = value
            If _val > _max Then Me.Value = _max
        End Set
    End Property

    Public Property Increment As Decimal
        Get
            Return _inc
        End Get
        Set(value As Decimal)
            If value > 0D Then _inc = value
        End Set
    End Property

    Public Property DecimalPlaces As Integer
        Get
            Return _decimals
        End Get
        Set(value As Integer)
            _decimals = Math.Max(0, value)
            SyncText()
        End Set
    End Property

    Public Property Value As Decimal
        Get
            Return _val
        End Get
        Set(value As Decimal)
            Dim v As Decimal = value
            If v < _min Then v = _min
            If v > _max Then v = _max
            If v <> _val Then
                _val = v
                SyncText()
                RaiseEvent ValueChanged(Me, EventArgs.Empty)
            Else
                SyncText()
            End If
        End Set
    End Property

    Private Sub LayoutBox()
        box.Location = New Point(8, (Height - box.Height) \ 2)
        box.Width = Math.Max(10, Width - ArrowW - 14)
    End Sub

    Private Sub SyncText()
        updatingText = True
        box.Text = _val.ToString("F" & _decimals, Globalization.CultureInfo.CurrentCulture)
        updatingText = False
    End Sub

    Private Sub CommitText()
        If updatingText Then Return
        Dim v As Decimal
        If Decimal.TryParse(box.Text, Globalization.NumberStyles.Number,
                            Globalization.CultureInfo.CurrentCulture, v) OrElse
           Decimal.TryParse(box.Text, Globalization.NumberStyles.Number,
                            Globalization.CultureInfo.InvariantCulture, v) Then
            Me.Value = v
        Else
            SyncText()
        End If
    End Sub

    Private Sub Box_KeyDown(sender As Object, e As KeyEventArgs)
        If e.KeyCode = Keys.Enter Then
            CommitText()
            e.Handled = True
            e.SuppressKeyPress = True
        ElseIf e.KeyCode = Keys.Up Then
            Me.Value = _val + _inc
            e.Handled = True
        ElseIf e.KeyCode = Keys.Down Then
            Me.Value = _val - _inc
            e.Handled = True
        End If
    End Sub

    Private Sub Box_Leave(sender As Object, e As EventArgs)
        CommitText()
    End Sub

    Protected Overrides Sub OnResize(e As EventArgs)
        MyBase.OnResize(e)
        LayoutBox()
    End Sub

    Protected Overrides Sub OnEnabledChanged(e As EventArgs)
        MyBase.OnEnabledChanged(e)
        box.Enabled = Enabled
        box.BackColor = UiTheme.BgField
        box.ForeColor = If(Enabled, UiTheme.Txt, UiTheme.TxtDim)
        Invalidate()
    End Sub

    ' Riapplica i colori del tema corrente alla TextBox interna.
    Public Sub RefreshTheme()
        BackColor = UiTheme.BgField
        box.BackColor = UiTheme.BgField
        box.ForeColor = If(Enabled, UiTheme.Txt, UiTheme.TxtDim)
        Invalidate()
    End Sub

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        Dim z As Integer = 0
        If e.X >= Width - ArrowW Then
            z = If(e.Y < Height \ 2, 1, 2)
        End If
        If z <> hoverZone Then
            hoverZone = z
            Invalidate()
        End If
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        MyBase.OnMouseLeave(e)
        If hoverZone <> 0 Then
            hoverZone = 0
            Invalidate()
        End If
    End Sub

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        If Not Enabled Then Return
        If e.X >= Width - ArrowW Then
            CommitText()
            If e.Y < Height \ 2 Then
                Me.Value = _val + _inc
            Else
                Me.Value = _val - _inc
            End If
        End If
    End Sub

    Protected Overrides Sub OnMouseWheel(e As MouseEventArgs)
        MyBase.OnMouseWheel(e)
        PerformWheel(e.Delta)
    End Sub

    ' Rotella inoltrata dal message filter quando il cursore e' sopra il controllo.
    Public Sub PerformWheel(delta As Integer)
        If Not Enabled Then Return
        CommitText()
        Me.Value = _val + If(delta > 0, _inc, -_inc)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        e.Graphics.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias
        e.Graphics.PixelOffsetMode = Drawing2D.PixelOffsetMode.HighQuality

        Dim parentBg As Color = If(Parent IsNot Nothing, Parent.BackColor, UiTheme.BgSidebar)
        e.Graphics.Clear(parentBg)

        Dim r As New RectangleF(0.5F, 0.5F, Width - 1.0F, Height - 1.0F)
        Using gp = UiTheme.RoundedRect(r, 5.0F)
            Using b As New SolidBrush(UiTheme.BgField)
                e.Graphics.FillPath(b, gp)
            End Using
            Using p As New Pen(UiTheme.Border, 1.0F)
                e.Graphics.DrawPath(p, gp)
            End Using
        End Using

        ' Zone frecce (evidenziate al passaggio).
        Dim ax As Integer = Width - ArrowW
        If hoverZone = 1 Then
            Using b As New SolidBrush(UiTheme.BgFieldHi)
                e.Graphics.FillRectangle(b, ax, 2, ArrowW - 3, Height \ 2 - 2)
            End Using
        ElseIf hoverZone = 2 Then
            Using b As New SolidBrush(UiTheme.BgFieldHi)
                e.Graphics.FillRectangle(b, ax, Height \ 2, ArrowW - 3, Height \ 2 - 3)
            End Using
        End If

        Dim arrowCol As Color = If(Enabled, UiTheme.TxtDim, UiTheme.Border)
        Using p As New Pen(arrowCol, 1.4F)
            p.StartCap = Drawing2D.LineCap.Round
            p.EndCap = Drawing2D.LineCap.Round
            Dim cxp As Single = ax + (ArrowW - 3) / 2.0F
            ' freccia su
            e.Graphics.DrawLines(p, New PointF() {
                New PointF(cxp - 3.2F, Height * 0.25F + 1.6F),
                New PointF(cxp, Height * 0.25F - 1.6F),
                New PointF(cxp + 3.2F, Height * 0.25F + 1.6F)})
            ' freccia giu
            e.Graphics.DrawLines(p, New PointF() {
                New PointF(cxp - 3.2F, Height * 0.75F - 1.6F),
                New PointF(cxp, Height * 0.75F + 1.6F),
                New PointF(cxp + 3.2F, Height * 0.75F - 1.6F)})
        End Using
    End Sub
End Class

' ============================================================
'  ComboBox a tema con tendina scura. Espone l'interfaccia usata dal
'  codice: Items (AddRange), SelectedItem, SelectedIndex, DropDownStyle
'  (ignorata), evento SelectedIndexChanged.
' ============================================================
Public Class ThemedComboBox
    Inherits Control

    Public ReadOnly Property Items As New List(Of Object)
    Private _selIndex As Integer = -1
    Private hovering As Boolean = False
    Private dropDown As ToolStripDropDown = Nothing

    Public Event SelectedIndexChanged As EventHandler

    ' Compatibilita' con ComboBox: la tendina e' sempre in stile DropDownList.
    Public Property DropDownStyle As ComboBoxStyle = ComboBoxStyle.DropDownList

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or
                 ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.OptimizedDoubleBuffer Or
                 ControlStyles.ResizeRedraw Or
                 ControlStyles.Selectable, True)
        Height = 26
        Cursor = Cursors.Hand
        BackColor = UiTheme.BgField
        ForeColor = UiTheme.Txt
    End Sub

    Public Property SelectedIndex As Integer
        Get
            Return _selIndex
        End Get
        Set(value As Integer)
            Dim v As Integer = value
            If v < -1 Then v = -1
            If v >= Items.Count Then v = Items.Count - 1
            If v <> _selIndex Then
                _selIndex = v
                Invalidate()
                RaiseEvent SelectedIndexChanged(Me, EventArgs.Empty)
            End If
        End Set
    End Property

    Public Property SelectedItem As Object
        Get
            If _selIndex >= 0 AndAlso _selIndex < Items.Count Then Return Items(_selIndex)
            Return Nothing
        End Get
        Set(value As Object)
            If value Is Nothing Then
                SelectedIndex = -1
                Return
            End If
            Dim wanted As String = value.ToString()
            For i As Integer = 0 To Items.Count - 1
                If String.Equals(Items(i).ToString(), wanted, StringComparison.Ordinal) Then
                    SelectedIndex = i
                    Return
                End If
            Next
        End Set
    End Property

    Protected Overrides Sub OnMouseEnter(e As EventArgs)
        MyBase.OnMouseEnter(e)
        hovering = True
        Invalidate()
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        MyBase.OnMouseLeave(e)
        hovering = False
        Invalidate()
    End Sub

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        If Not Enabled Then Return
        Focus()
        OpenDropDown()
    End Sub

    Protected Overrides Sub OnMouseWheel(e As MouseEventArgs)
        MyBase.OnMouseWheel(e)
        PerformWheel(e.Delta)
    End Sub

    ' Rotella (diretta o inoltrata dal message filter): scorre le voci.
    Public Sub PerformWheel(delta As Integer)
        If Not Enabled OrElse Items.Count = 0 Then Return
        If delta > 0 Then
            If _selIndex > 0 Then
                SelectedIndex = _selIndex - 1
            ElseIf _selIndex < 0 Then
                SelectedIndex = 0
            End If
        Else
            If _selIndex < Items.Count - 1 Then SelectedIndex = _selIndex + 1
        End If
    End Sub

    Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
        MyBase.OnKeyDown(e)
        If Not Enabled Then Return
        If e.KeyCode = Keys.Down AndAlso e.Alt Then
            OpenDropDown()
            e.Handled = True
        ElseIf e.KeyCode = Keys.Down Then
            If _selIndex < Items.Count - 1 Then SelectedIndex = _selIndex + 1
            e.Handled = True
        ElseIf e.KeyCode = Keys.Up Then
            If _selIndex > 0 Then SelectedIndex = _selIndex - 1
            e.Handled = True
        ElseIf e.KeyCode = Keys.Enter OrElse e.KeyCode = Keys.Space Then
            OpenDropDown()
            e.Handled = True
        End If
    End Sub

    Private Sub OpenDropDown()
        If Items.Count = 0 Then Return
        If dropDown IsNot Nothing AndAlso dropDown.Visible Then
            dropDown.Close()
            Return
        End If

        Dim lst As New ListBox()
        lst.BorderStyle = BorderStyle.None
        lst.BackColor = UiTheme.BgField
        lst.ForeColor = UiTheme.Txt
        lst.Font = Font
        lst.DrawMode = DrawMode.OwnerDrawFixed
        lst.ItemHeight = 20
        lst.IntegralHeight = False
        For Each it In Items
            lst.Items.Add(it.ToString())
        Next
        lst.SelectedIndex = _selIndex
        lst.Width = Math.Max(Width - 2, 60)
        lst.Height = Math.Min(lst.ItemHeight * Items.Count + 4, 320)

        AddHandler lst.DrawItem, AddressOf List_DrawItem
        AddHandler lst.MouseMove, Sub(s, ev)
                                      Dim idx = lst.IndexFromPoint(ev.Location)
                                      If idx >= 0 AndAlso idx <> lst.SelectedIndex Then lst.SelectedIndex = idx
                                  End Sub
        AddHandler lst.MouseUp, Sub(s, ev)
                                    Dim idx = lst.IndexFromPoint(ev.Location)
                                    If idx >= 0 Then
                                        SelectedIndex = idx
                                        dropDown.Close()
                                    End If
                                End Sub
        AddHandler lst.KeyDown, Sub(s, ev)
                                    If ev.KeyCode = Keys.Enter Then
                                        If lst.SelectedIndex >= 0 Then SelectedIndex = lst.SelectedIndex
                                        dropDown.Close()
                                    ElseIf ev.KeyCode = Keys.Escape Then
                                        dropDown.Close()
                                    End If
                                End Sub

        Dim border As New Panel()
        border.BackColor = UiTheme.Border
        border.Padding = New Padding(1)
        border.Width = lst.Width + 2
        border.Height = lst.Height + 2
        lst.Dock = DockStyle.Fill
        border.Controls.Add(lst)

        Dim host As New ToolStripControlHost(border)
        host.Margin = Padding.Empty
        host.Padding = Padding.Empty
        host.AutoSize = False
        host.Size = border.Size

        dropDown = New ToolStripDropDown()
        dropDown.Padding = Padding.Empty
        dropDown.Margin = Padding.Empty
        dropDown.AutoSize = False
        dropDown.Size = border.Size
        dropDown.DropShadowEnabled = True
        dropDown.Items.Add(host)

        dropDown.Show(Me, New Point(0, Height))
        lst.Focus()
    End Sub

    Private Sub List_DrawItem(sender As Object, e As DrawItemEventArgs)
        If e.Index < 0 Then Return
        Dim lst = DirectCast(sender, ListBox)
        Dim selected As Boolean = (e.State And DrawItemState.Selected) = DrawItemState.Selected

        Using b As New SolidBrush(If(selected, UiTheme.BgFieldHi, UiTheme.BgField))
            e.Graphics.FillRectangle(b, e.Bounds)
        End Using
        Using tb As New SolidBrush(If(selected, UiTheme.AccentHi, UiTheme.Txt))
            e.Graphics.DrawString(lst.Items(e.Index).ToString(), lst.Font, tb,
                                  e.Bounds.X + 6, e.Bounds.Y + 2)
        End Using
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        e.Graphics.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias
        e.Graphics.PixelOffsetMode = Drawing2D.PixelOffsetMode.HighQuality

        Dim parentBg As Color = If(Parent IsNot Nothing, Parent.BackColor, UiTheme.BgSidebar)
        e.Graphics.Clear(parentBg)

        Dim r As New RectangleF(0.5F, 0.5F, Width - 1.0F, Height - 1.0F)
        Using gp = UiTheme.RoundedRect(r, 5.0F)
            Using b As New SolidBrush(If(hovering AndAlso Enabled, UiTheme.BgFieldHi, UiTheme.BgField))
                e.Graphics.FillPath(b, gp)
            End Using
            Using p As New Pen(If(hovering AndAlso Enabled, UiTheme.Accent, UiTheme.Border), 1.0F)
                e.Graphics.DrawPath(p, gp)
            End Using
        End Using

        Dim txt As String = If(SelectedItem Is Nothing, "", SelectedItem.ToString())
        Dim txtCol As Color = If(Enabled, UiTheme.Txt, UiTheme.TxtDim)
        Dim txtRect As New Rectangle(8, 0, Width - 28, Height)
        TextRenderer.DrawText(e.Graphics, txt, Font, txtRect, txtCol,
                              TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis)

        ' Chevron
        Dim chevCol As Color = If(Enabled, UiTheme.TxtDim, UiTheme.Border)
        Using p As New Pen(chevCol, 1.6F)
            p.StartCap = Drawing2D.LineCap.Round
            p.EndCap = Drawing2D.LineCap.Round
            Dim cxp As Single = Width - 15
            Dim cyp As Single = Height / 2.0F - 2.0F
            e.Graphics.DrawLines(p, New PointF() {
                New PointF(cxp - 4.0F, cyp),
                New PointF(cxp, cyp + 4.5F),
                New PointF(cxp + 4.0F, cyp)})
        End Using
    End Sub
End Class

' ============================================================
'  Scrollbar verticale a tema (thumb navy, hover chiaro, drag accento).
'  Usata dalla sidebar al posto della scrollbar di sistema.
' ============================================================
Public Class ThemedVScrollBar
    Inherits Control

    Private _content As Integer = 0
    Private _viewport As Integer = 0
    Private _val As Integer = 0
    Private dragging As Boolean = False
    Private dragOffset As Integer = 0
    Private hovering As Boolean = False

    Public Event ScrollChanged As EventHandler

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or
                 ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.OptimizedDoubleBuffer Or
                 ControlStyles.ResizeRedraw, True)
        Width = 8
        BackColor = UiTheme.BgSidebar
        Cursor = Cursors.Hand
    End Sub

    Public Property ContentSize As Integer
        Get
            Return _content
        End Get
        Set(value As Integer)
            _content = Math.Max(0, value)
            ClampValue()
            Invalidate()
        End Set
    End Property

    Public Property ViewportSize As Integer
        Get
            Return _viewport
        End Get
        Set(value As Integer)
            _viewport = Math.Max(0, value)
            ClampValue()
            Invalidate()
        End Set
    End Property

    Public ReadOnly Property MaxScroll As Integer
        Get
            Return Math.Max(0, _content - _viewport)
        End Get
    End Property

    Public Property Value As Integer
        Get
            Return _val
        End Get
        Set(value As Integer)
            Dim v As Integer = value
            If v < 0 Then v = 0
            If v > MaxScroll Then v = MaxScroll
            If v <> _val Then
                _val = v
                Invalidate()
                RaiseEvent ScrollChanged(Me, EventArgs.Empty)
            End If
        End Set
    End Property

    Private Sub ClampValue()
        If _val > MaxScroll Then Value = MaxScroll
    End Sub

    Private Function ThumbRect() As Rectangle
        Dim trackH As Integer = Math.Max(0, Height - 4)
        If _content <= 0 OrElse trackH <= 0 Then Return Rectangle.Empty

        Dim th As Integer = Math.Max(24, CInt(CLng(trackH) * _viewport \ Math.Max(_content, 1)))
        th = Math.Min(th, trackH)
        Dim travel As Integer = trackH - th
        Dim y As Integer = 2
        If MaxScroll > 0 AndAlso travel > 0 Then
            y = 2 + CInt(CLng(travel) * _val \ MaxScroll)
        End If
        Return New Rectangle(0, y, Width, th)
    End Function

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        Dim tr = ThumbRect()
        If tr.Contains(e.Location) Then
            dragging = True
            dragOffset = e.Y - tr.Y
        ElseIf e.Y < tr.Y Then
            Value = _val - _viewport
        Else
            Value = _val + _viewport
        End If
        Invalidate()
    End Sub

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        If dragging Then
            Dim tr = ThumbRect()
            Dim trackH As Integer = Math.Max(0, Height - 4)
            Dim travel As Integer = trackH - tr.Height
            If travel > 0 Then
                Dim y As Integer = e.Y - dragOffset - 2
                Value = CInt(CLng(Math.Max(0, Math.Min(travel, y))) * MaxScroll \ travel)
            End If
        End If
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        dragging = False
        Invalidate()
    End Sub

    Protected Overrides Sub OnMouseEnter(e As EventArgs)
        MyBase.OnMouseEnter(e)
        hovering = True
        Invalidate()
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        MyBase.OnMouseLeave(e)
        hovering = False
        Invalidate()
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        e.Graphics.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias
        e.Graphics.PixelOffsetMode = Drawing2D.PixelOffsetMode.HighQuality
        e.Graphics.Clear(BackColor)

        Dim tr = ThumbRect()
        If tr.IsEmpty Then Return

        Dim col As Color
        If dragging Then
            col = UiTheme.Accent
        ElseIf hovering Then
            col = Color.FromArgb(74, 70, 158)
        Else
            col = UiTheme.Border
        End If

        Using gp = UiTheme.RoundedRect(New RectangleF(tr.X + 0.5F, tr.Y + 0.5F, tr.Width - 1.0F, tr.Height - 1.0F), 3.5F)
            Using b As New SolidBrush(col)
                e.Graphics.FillPath(b, gp)
            End Using
        End Using
    End Sub
End Class
