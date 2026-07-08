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
Imports System.Windows.Forms

Public Module ThemeApplier

    Private ReadOnly ownerDrawnTabs As New HashSet(Of TabControl)
    Private ReadOnly paintedGroups As New HashSet(Of GroupBox)
    Private sharedRenderer As UiThemeToolStripRenderer = Nothing

    Public Sub Apply(root As Form)
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
            c.ForeColor = UiTheme.Txt
            c.BackColor = Color.Transparent

        ElseIf TypeOf c Is Label Then
            c.ForeColor = UiTheme.Txt
            c.BackColor = Color.Transparent

        ElseIf TypeOf c Is TextBox Then
            Dim t = DirectCast(c, TextBox)
            t.BackColor = UiTheme.BgField
            t.ForeColor = UiTheme.Txt
            t.BorderStyle = BorderStyle.FixedSingle
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
            End If
            tc.Invalidate()

        ElseIf TypeOf c Is ListView Then
            Dim lv = DirectCast(c, ListView)
            lv.BackColor = UiTheme.BgField
            lv.ForeColor = UiTheme.Txt
            lv.BorderStyle = BorderStyle.FixedSingle
            If lv.IsHandleCreated Then
                UiTheme.ApplyScrollBarTheme(lv.Handle)
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

        ElseIf TypeOf c Is Panel OrElse
               TypeOf c Is TabPage OrElse
               TypeOf c Is TableLayoutPanel OrElse
               TypeOf c Is FlowLayoutPanel OrElse
               TypeOf c Is SplitContainer OrElse
               TypeOf c Is UserControl Then
            c.BackColor = UiTheme.BgSidebar
            c.ForeColor = UiTheme.Txt
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
