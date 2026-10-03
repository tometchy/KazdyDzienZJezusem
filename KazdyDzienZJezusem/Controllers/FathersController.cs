using KazdyDzienZJezusem.Models;
using KazdyDzienZJezusem.Services;
using Microsoft.AspNetCore.Mvc;

namespace KazdyDzienZJezusem.Controllers;

[Route("Ojcowie")]
public sealed class FathersController(FathersRepository fathersRepository) : Controller
{
    [HttpGet("")]
    public IActionResult Index() =>
        View(new FatherIndexViewModel(fathersRepository.GetAuthors()));

    [HttpGet("{author}")]
    public IActionResult Author(string author)
    {
        var selectedAuthor = fathersRepository.FindAuthor(author);
        return selectedAuthor is null
            ? NotFound()
            : View(new FatherWorkListViewModel(
                selectedAuthor,
                fathersRepository.GetWorks(selectedAuthor)));
    }

    [HttpGet("{author}/{work}")]
    public IActionResult Work(string author, string work)
    {
        var selectedAuthor = fathersRepository.FindAuthor(author);
        var selectedWork = selectedAuthor is null
            ? null
            : fathersRepository.FindWork(selectedAuthor, work);
        if (selectedAuthor is null || selectedWork is null)
        {
            return NotFound();
        }

        return View(new FatherChapterListViewModel(
            selectedAuthor,
            selectedWork,
            fathersRepository.GetChapters(selectedAuthor, selectedWork)));
    }

    [HttpGet("{author}/{work}/{chapter:int:min(0)}")]
    public IActionResult Chapter(string author, string work, int chapter)
    {
        var selectedAuthor = fathersRepository.FindAuthor(author);
        var selectedWork = selectedAuthor is null
            ? null
            : fathersRepository.FindWork(selectedAuthor, work);
        if (selectedAuthor is null
            || selectedWork is null
            || !fathersRepository.HasChapter(selectedAuthor, selectedWork, chapter))
        {
            return NotFound();
        }

        var chapters = fathersRepository.GetChapters(selectedAuthor, selectedWork);
        var index = chapters.ToList().IndexOf(chapter);
        return View(new FatherChapterViewModel(
            selectedAuthor,
            selectedWork,
            chapter,
            fathersRepository.GetChapter(selectedAuthor, selectedWork, chapter),
            index > 0 ? chapters[index - 1] : null,
            index < chapters.Count - 1 ? chapters[index + 1] : null));
    }
}
