using API.Entities;

namespace API.Interfaces;

public interface IPhotoRepository
{
    Task<Photo?> GetPhotoById(int id);
    Task<IEnumerable<Photo>> GetUnapprovedPhotos();
    void RemovePhoto(Photo photo);
}
