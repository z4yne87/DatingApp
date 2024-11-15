using API.DTOs;
using API.Entities;
using API.Interfaces;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers;

public class AdminController(
    UserManager<AppUser> userManager,
    IUnitOfWork unitOfWork,
    IPhotoService photoService) : BaseApiController
{
    [Authorize(Policy = "RequireAdminRole")]
    [HttpGet("users-with-roles")]
    public async Task<ActionResult> GetUsersWithRoles()
    {
        var users = await userManager.Users
            .OrderBy(x => x.UserName)
            .Select(x => new
            {
                x.Id,
                Username = x.UserName,
                Roles = x.UserRoles.Select(r => r.Role.Name).ToList()
            }).ToListAsync();

        return Ok(users);
    }

    [Authorize(Policy = "RequireAdminRole")]
    [HttpPost("edit-roles/{username}")]
    public async Task<ActionResult> EditRoles(string username, string roles)
    {
        if (string.IsNullOrEmpty(roles)) return BadRequest("You must select at least one role");

        var selectedRoles = roles.Split(",").ToArray();

        var user = await userManager.FindByNameAsync(username);

        if (user == null) return BadRequest("User not found");

        var userRoles = await userManager.GetRolesAsync(user);

        var result = await userManager.AddToRolesAsync(user, selectedRoles.Except(userRoles));

        if (!result.Succeeded) return BadRequest("Failed to add to roles");

        result = await userManager.RemoveFromRolesAsync(user, userRoles.Except(selectedRoles));

        if (!result.Succeeded) return BadRequest("Failed to remove from roles");

        return Ok(await userManager.GetRolesAsync(user));
    }

    [Authorize(Policy = "ModeratePhotoRole")]
    [HttpGet("photos-to-moderate")]
    public async Task<ActionResult> GetPhotosToModerate()
    {
        IEnumerable<Photo> unapprovedPhotos = await unitOfWork.PhotoRepository.GetUnapprovedPhotos();

        List<PhotoForApprovalDto> approvalDtos = [];

        foreach (Photo photo in unapprovedPhotos)
        {
            PhotoForApprovalDto approvalDto = new() {
                Id = photo.Id,
                Url = photo.Url,
                IsApproved = photo.IsApproved
            };

            AppUser? user = await unitOfWork.UserRepository.GetUserByIdAsync(photo.AppUserId);

            if (user != null)
            {
                approvalDto.Username = user.UserName;
            }

            approvalDtos.Add(approvalDto);
        }

        return Ok(approvalDtos);
    }

    [Authorize(Policy = "ModeratePhotoRole")]
    [HttpPost("approve-photo/{photoId}")]
    public async Task<ActionResult> ApprovePhoto(int photoId)
    {
        Photo? photo = await unitOfWork.PhotoRepository.GetPhotoById(photoId);

        if (photo == null) return BadRequest("Could not find photo");

        photo.IsApproved = true;

        bool userHasMainPhoto = false;

        AppUser? user = await unitOfWork.UserRepository.GetUserByIdAsync(photo.AppUserId);

        if (user != null)
        {
            foreach (Photo userPhoto in user.Photos)
            {
                if (userPhoto.IsMain)
                {
                    userHasMainPhoto = true;
                    break;
                }
            }

            if (!userHasMainPhoto)
            {
                photo.IsMain = true;
            }
        }

        if (await unitOfWork.Complete())
        {
            return Ok();
        }

        return BadRequest("Problem approving photo");
    }

    [Authorize(Policy = "ModeratePhotoRole")]
    [HttpPost("reject-photo/{photoId}")]
    public async Task<ActionResult> RejectPhoto(int photoId)
    {
        Photo? photo = await unitOfWork.PhotoRepository.GetPhotoById(photoId);

        if (photo == null)
        {
            return BadRequest("Could not find photo");
        }

        if (photo.IsApproved)
        {
            return BadRequest("Cannot reject approved photo");
        }

        if (photo.PublicId != null)
        {
            var result = await photoService.DeletePhotoAsync(photo.PublicId);
            if (result.Error != null) return BadRequest(result.Error.Message);
        }

        unitOfWork.PhotoRepository.RemovePhoto(photo);

        if (await unitOfWork.Complete())
        {
            return Ok();
        }

        return BadRequest("Problem rejecting photo");
    }
}
