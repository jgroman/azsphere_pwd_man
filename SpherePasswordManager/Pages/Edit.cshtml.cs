using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

using SpherePasswordManager.Models;
using SpherePasswordManager.Services;

namespace SpherePasswordManager.Pages
{
    [BindProperties]
    public class EditModel : PageModel
    {
        public int Id { get; set; }

        [Required]
        [StringLength(30, ErrorMessage = "Name cannot be longer than 30 characters.")]
        [RegularExpression(@".*[a-zA-Z0-9\-]$", ErrorMessage = "Alphanumeric characters and dashes only.")]
        [PageRemote(
            PageHandler = "validatename",
            AdditionalFields = nameof(Id) + ", __RequestVerificationToken",
            HttpMethod = "post",
            ErrorMessage = "Name already exists."
        )]
        public string Name { get; set; }

        [Required]
        [StringLength(50, ErrorMessage = "Password cannot be longer than 50 characters.")]
        public string Password { get; set; }

        [StringLength(50, ErrorMessage = "Username cannot be longer than 50 characters.")]
        public string Username { get; set; }

        [StringLength(100, ErrorMessage = "URL cannot be longer than 100 characters.")]
        public string Uri { get; set; }


        private readonly IItemService _itemService;

        public EditModel(IItemService itemService)
        {
            _itemService = itemService;
        }

        public IActionResult OnGetCreate()
        {
            Id = 0;
            Name = "";
            Username = "";
            Password = "";
            Uri = "";

            return Page();
        }

        public async Task<IActionResult> OnGetUpdate(int id)
        {
            if (id == 0)
            {
                return RedirectToPage("/Index");
            }
            
            Item item = await _itemService.ReadAsync(id);
            if (item == null)
            {
                return RedirectToPage("/Index");
            }


            Id = id;
            Name = item.Name;
            Username = item.Username;
            Password = item.Password;
            Uri = item.Uri;

            return Page();
        }

        public async Task<IActionResult> OnGetDeleteAsync(int id)
        {
            await _itemService.DeleteAsync(id);
            return RedirectToPage("/Index");
        }

        public async Task<IActionResult> OnPostValidateNameAsync(int Id, string Name)
        {
            if (Id != 0)
            {
                // Updating existing item, no name checks if name not modified
                Item originalItem = await _itemService.ReadAsync(Id);
                if (Name == originalItem.Name)
                {
                    return new JsonResult(true);
                }
            }

            // Name changed or creating new item
            bool doesNameExist = await _itemService.CheckItemNameExistsAsync(Name);
            return new JsonResult(!doesNameExist);
        }


        public async Task<IActionResult> OnPostSubmitAsync()
        {
            System.Diagnostics.Debug.WriteLine($"******* Submit {Name}, {Id}");

            Item item = new Item()
            {
                Id = Id,
                Name = Name,
                Username = Username,
                Password = Password,
                Uri = Uri
            };

            if (Id == 0)
            {
                // Create new item
                await _itemService.CreateAsync(item);
            }
            else
            {
                // Update existing item
                await _itemService.UpdateAsync(item);
            }

            return RedirectToPage("Index");
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            await _itemService.DeleteAsync(id);
            return RedirectToPage("/Index");
        }
    }
}
