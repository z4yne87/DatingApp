import { Component, inject, OnInit } from '@angular/core';
import { AdminService } from '../../_services/admin.service';
import { Photo } from '../../_models/photo';

@Component({
  selector: 'app-photo-management',
  standalone: true,
  imports: [],
  templateUrl: './photo-management.component.html',
  styleUrl: './photo-management.component.css'
})
export class PhotoManagementComponent implements OnInit {
  private adminService = inject(AdminService);
  photosToModerate: Photo[] = [];

  ngOnInit(): void {
    this.getPhotosToModerate();
  }

  getPhotosToModerate() {
    this.adminService.getPhotosForApproval().subscribe({
      next: photos => this.photosToModerate = photos
    });
  }

  approvePhoto(photoId: number) {
    this.adminService.approvePhoto(photoId).subscribe({
      next: _ => {
        this.photosToModerate = this.photosToModerate.filter(p => p.id !== photoId);
      }
    });
  }

  rejectPhoto(photoId: number) {
    this.adminService.rejectPhoto(photoId).subscribe({
      next: _ => {
        this.photosToModerate = this.photosToModerate.filter(p => p.id !== photoId);
      }
    });
  }
}
