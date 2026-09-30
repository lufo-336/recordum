# Passata estetica (Fase C rimodernizzazione UI) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Uniformare il chrome: toggle di vista come pill segmentate (Home tabs + calendario), ricerca Home con icona e placeholder funzionante, scrollbar scure sottili a tema. Nessun cambio funzionale.

**Architecture:** Tutto styling XAML. Un nuovo stile keyed condiviso `Recordum.Segment` (RadioButton → pill senza pallino) in `BaseStyles.xaml`, adottato dalla Home e dal calendario. Stile implicito `ScrollBar` flat (thumb arrotondato, niente frecce ingombranti). Search box incapsulata con icona Lucide + watermark via trigger su `Text`.

**Tech Stack:** WPF styles/ControlTemplate; nessun codice C#; nessun test nuovo (suite invariata = regression gate).

**Spec:** `docs/superpowers/archive/specs/2026-06-11-ui-modernization-plan-design.md` §Fase C.

---

### Task 1: stile `Recordum.Segment` + ScrollBar flat (BaseStyles)

**Files:** Modify `src/Recordum.WPF/Resources/BaseStyles.xaml`

- [ ] **Step 1**: dopo lo stile implicito `RadioButton`, aggiungere il segment keyed:

```xml
    <!-- Segmented pill for view toggles (Home tabs, calendar view modes): a RadioButton without
         the bullet — flat pill, checked = elevated bg + accent underline-ish border. -->
    <Style x:Key="Recordum.Segment" TargetType="{x:Type RadioButton}">
        <Setter Property="Foreground" Value="{DynamicResource Brush.TextMuted}"/>
        <Setter Property="Padding" Value="11,4"/>
        <Setter Property="Cursor" Value="Hand"/>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="{x:Type RadioButton}">
                    <Border x:Name="bd" CornerRadius="4" Padding="{TemplateBinding Padding}"
                            Background="Transparent" BorderThickness="1" BorderBrush="Transparent">
                        <ContentPresenter VerticalAlignment="Center" RecognizesAccessKey="True"/>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="bd" Property="Background" Value="{DynamicResource Brush.SurfaceElevated}"/>
                        </Trigger>
                        <Trigger Property="IsChecked" Value="True">
                            <Setter TargetName="bd" Property="Background" Value="{DynamicResource Brush.SurfaceElevated}"/>
                            <Setter TargetName="bd" Property="BorderBrush" Value="{DynamicResource Brush.Border}"/>
                            <Setter Property="Foreground" Value="{DynamicResource Brush.Text}"/>
                            <Setter Property="FontWeight" Value="SemiBold"/>
                        </Trigger>
                        <Trigger Property="IsKeyboardFocused" Value="True">
                            <Setter TargetName="bd" Property="BorderBrush" Value="{DynamicResource Brush.Accent}"/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
```

- [ ] **Step 2**: in coda al file (sezione nuova "Scrollbars"), stile flat implicito:

```xml
    <!-- Scrollbars — thin, flat, themed (Fase C). Track transparent, rounded thumb, no arrows. -->
    <Style TargetType="{x:Type ScrollBar}">
        <Setter Property="Background" Value="Transparent"/>
        <Setter Property="Width" Value="10"/>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="{x:Type ScrollBar}">
                    <Grid Background="Transparent">
                        <Track x:Name="PART_Track" IsDirectionReversed="True">
                            <Track.DecreaseRepeatButton>
                                <RepeatButton Command="ScrollBar.PageUpCommand" Opacity="0" Focusable="False"/>
                            </Track.DecreaseRepeatButton>
                            <Track.Thumb>
                                <Thumb>
                                    <Thumb.Template>
                                        <ControlTemplate TargetType="Thumb">
                                            <Border CornerRadius="4" Margin="2"
                                                    Background="{DynamicResource Brush.Border}"/>
                                        </ControlTemplate>
                                    </Thumb.Template>
                                </Thumb>
                            </Track.Thumb>
                            <Track.IncreaseRepeatButton>
                                <RepeatButton Command="ScrollBar.PageDownCommand" Opacity="0" Focusable="False"/>
                            </Track.IncreaseRepeatButton>
                        </Track>
                    </Grid>
                    <ControlTemplate.Triggers>
                        <Trigger Property="Orientation" Value="Horizontal">
                            <Setter TargetName="PART_Track" Property="IsDirectionReversed" Value="False"/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
        <Style.Triggers>
            <Trigger Property="Orientation" Value="Horizontal">
                <Setter Property="Width" Value="Auto"/>
                <Setter Property="Height" Value="10"/>
            </Trigger>
        </Style.Triggers>
    </Style>
```

ATTENZIONE: l'header di BaseStyles avverte che RepeatButton implicito resta Foreground-only proprio per non corrompere le frecce ScrollBar — col template custom le frecce spariscono del tutto, quindi il vincolo decade per le scrollbar. Verificare che DataGrid/ScrollViewer esistenti restino usabili.

- [ ] **Step 3**: build 0/0, commit `style(wpf): segmented pill style + flat themed scrollbars`.

---

### Task 2: Home toolbar

**Files:** Modify `src/Recordum.WPF/Views/HomeView.xaml`

- [ ] **Step 1**: i 3 RadioButton `GroupName="HomeTab"` prendono `Style="{StaticResource Recordum.Segment}"`, dentro un Border-contenitore pill-group:

```xml
            <Border Grid.Column="1" HorizontalAlignment="Center" VerticalAlignment="Center"
                    Background="{DynamicResource Brush.Background}" CornerRadius="6" Padding="3">
                <StackPanel Orientation="Horizontal">
                    <RadioButton GroupName="HomeTab" Style="{StaticResource Recordum.Segment}"
                                 Content="{x:Static res:Strings.Home_Tab_Active}"
                                 IsChecked="{Binding ActiveTab, Mode=OneWay, Converter={StaticResource EnumToBool}, ConverterParameter={x:Static vm:HomeTab.Active}}"
                                 Command="{Binding SwitchTabCommand}"
                                 CommandParameter="{x:Static vm:HomeTab.Active}"/>
                    <RadioButton GroupName="HomeTab" Style="{StaticResource Recordum.Segment}" Margin="2,0,0,0"
                                 Content="{x:Static res:Strings.Home_Tab_Archived}"
                                 IsChecked="{Binding ActiveTab, Mode=OneWay, Converter={StaticResource EnumToBool}, ConverterParameter={x:Static vm:HomeTab.Archived}}"
                                 Command="{Binding SwitchTabCommand}"
                                 CommandParameter="{x:Static vm:HomeTab.Archived}"/>
                    <RadioButton GroupName="HomeTab" Style="{StaticResource Recordum.Segment}" Margin="2,0,0,0"
                                 Content="{x:Static res:Strings.Home_Tab_Trash}"
                                 IsChecked="{Binding ActiveTab, Mode=OneWay, Converter={StaticResource EnumToBool}, ConverterParameter={x:Static vm:HomeTab.Trash}}"
                                 Command="{Binding SwitchTabCommand}"
                                 CommandParameter="{x:Static vm:HomeTab.Trash}"/>
                </StackPanel>
            </Border>
```

- [ ] **Step 2**: search box con icona + watermark funzionante (sostituisce il TextBox nudo col Tag inerte):

```xml
                <Grid VerticalAlignment="Center">
                    <TextBox x:Name="SearchBox" Width="200" Padding="26,4,6,4"
                             Text="{Binding SearchText, UpdateSourceTrigger=PropertyChanged}"/>
                    <Path Width="13" Height="13" Margin="8,0,0,0" Stretch="Uniform"
                          HorizontalAlignment="Left" VerticalAlignment="Center" IsHitTestVisible="False"
                          Stroke="{DynamicResource Brush.TextMuted}" StrokeThickness="1.7"
                          StrokeStartLineCap="Round" StrokeEndLineCap="Round"
                          Data="{StaticResource Icon.Search}"/>
                    <TextBlock Text="{x:Static res:Strings.Home_Filter_SearchPlaceholder}"
                               Margin="28,0,0,0" VerticalAlignment="Center" IsHitTestVisible="False"
                               Foreground="{DynamicResource Brush.TextMuted}"
                               FontSize="{StaticResource FontSize.Small}">
                        <TextBlock.Style>
                            <Style TargetType="TextBlock">
                                <Setter Property="Visibility" Value="Collapsed"/>
                                <Style.Triggers>
                                    <DataTrigger Binding="{Binding Text, ElementName=SearchBox}" Value="">
                                        <Setter Property="Visibility" Value="Visible"/>
                                    </DataTrigger>
                                </Style.Triggers>
                            </Style>
                        </TextBlock.Style>
                    </TextBlock>
                </Grid>
```

- [ ] **Step 3**: build, commit `style(wpf): Home toolbar — segmented tabs + search with icon/placeholder`.

---

### Task 3: calendario su Segment

**Files:** Modify `src/Recordum.WPF/Views/HabitScreen/Calendar/CalendarNavigator.xaml`

- [ ] **Step 1**: i 3 RadioButton `GroupName="calview"` e i 2 `GroupName="calsub"` prendono `Style="{StaticResource Recordum.Segment}"` (margini esistenti conservati; i `calsub` possono ridurre Padding a `8,3` via setter inline se risultano ingombranti).
- [ ] **Step 2**: build, commit `style(wpf): calendar view toggles on the segmented pill style`.

---

### Task 4: verifica visiva + tracker

- [ ] skill `run` + checklist: Home tabs pill (stato attivo leggibile, hover); ricerca con icona e placeholder che sparisce digitando; scrollbar scure in Home/Detail/Charts/Impostazioni (drag thumb + page-click funzionanti); calendario Settimana/Mese/Anno + Mesi/Heatmap a pill; entrambi i temi.
- [ ] aggiornare `docs/phase-4-issues.md` + memoria; commit docs.

---

## Self-review
- Spec §C: Home toolbar (T2: pill + search; sort ComboBox già flat dal theming Pass 2 — invariato di proposito; "+ Nuova" già coerente), scrollbar (T1), RadioButton-nudi del chrome → pill (T2+T3; quelli nei form restano classici come da spec). Audit hover/spaziature: assorbito nella checklist visiva di T4 — eventuali fix puntuali emersi diventano commit dedicati.
- Tipi/chiavi coerenti: `Recordum.Segment` unico nome; `Icon.Search` esiste in Icons.xaml.
