export interface User {
  id: number;
  firstName: string;
  lastName: string;
  email: string;
  isActive: boolean;
  createdAt: string;
  lastActive?: string;
  lastLogin?: string;
  roles: string[];
}

export interface CreateUser {
  firstName: string;
  lastName: string;
  email: string;
  isActive: boolean;
  roles: string[];
}

export interface UpdateUser {
  id: number;
  firstName: string;
  lastName: string;
  email: string;
  isActive: boolean;
  roles: string[];
}
