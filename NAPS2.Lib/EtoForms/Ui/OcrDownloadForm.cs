using Eto.Drawing;
using Eto.Forms;
using NAPS2.EtoForms.Layout;
using NAPS2.EtoForms.Widgets;
using NAPS2.Ocr;

namespace NAPS2.EtoForms.Ui;

public class OcrDownloadForm : EtoDialogBase
{
    private readonly TesseractLanguageManager _tesseractLanguageManager;

    private readonly IListView<Language> _languageList =
        EtoPlatform.Current.CreateListView(new OcrLanguagesListViewBehavior());
    private readonly Label _downloadSize = new();
    private readonly Button _downloadButton;

    public OcrDownloadForm(Naps2Config config, TesseractLanguageManager tesseractLanguageManager) : base(config)
    {
        _tesseractLanguageManager = tesseractLanguageManager;

        var initialSelection = new HashSet<string>();
        // TODO: We used to select old installed languages here, maybe we could do it again if we get new lang data
        if (!_tesseractLanguageManager.InstalledLanguages.Any())
        {
            // Fresh install, so pre-select English as a sensible default
            initialSelection.Add("eng");
        }

        // Populate the list of language options
        // Special case for English: sorted to the top of the list
        _languageList.SetItems(_tesseractLanguageManager.NotInstalledLanguages
            .OrderBy(x => x.Code == "eng" ? "AAA" : x.Name));
        _languageList.Selection = ListSelection.From(initialSelection.Select(_tesseractLanguageManager.GetLanguage));

        _downloadButton = C.Button(UiStrings.Download, Download);
        _languageList.SelectionChanged += (_, _) => UpdateView();

        UpdateView();
    }

    protected override void BuildLayout()
    {
        Title = UiStrings.OcrDownloadFormTitle;
        Icon = new Icon(1f, Icons.text_small.ToEtoImage());

        FormStateController.RestoreFormState = false;
        FormStateController.DefaultExtraLayoutSize = new Size(300, 300);

        LayoutController.Content = L.Column(
            C.Label(UiStrings.OcrDownloadSummaryText),
            C.Spacer(),
            C.Label(UiStrings.OcrSelectLanguageLabel),
            _languageList.Control.Scale(),
            C.Spacer(),
            L.Row(
                _downloadSize.Scale().AlignCenter(),
                _downloadButton,
                C.CancelButton(this)
            )
        );
    }

    private void UpdateView()
    {
        var selected = SelectedLanguageComponents;
        double downloadSize = _tesseractLanguageManager.LanguageComponents
            .Where(x => selected.Contains(x.Id))
            .Select(x => x.DownloadInfo.Size)
            .Sum();

        _downloadSize.Text = string.Format(MiscResources.EstimatedDownloadSize, downloadSize.ToString("f1"));

        _downloadButton.Enabled = _languageList.Selection.Any();
    }

    private HashSet<string> SelectedLanguageComponents
    {
        get { return new HashSet<string>(_languageList.Selection.Select(lang => $"ocr-{lang.Code}")); }
    }

    private void Download()
    {
        var progressForm = FormFactory.Create<DownloadProgressForm>();

        var selected = SelectedLanguageComponents;
        foreach (var langComponent in _tesseractLanguageManager.LanguageComponents.Where(x => selected.Contains(x.Id)))
        {
            progressForm.Controller.QueueFile(langComponent);
        }

        Close();
        progressForm.ShowModal();
    }
}