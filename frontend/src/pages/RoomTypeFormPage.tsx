import { useEffect } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useNavigate, useParams } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { createRoomType, getRoomType, updateRoomType } from "../features/roomTypes/roomTypeService";

const roomTypeSchema = z.object({
  name: z.string().min(1, "Name is required").max(100),
  description: z.string().min(1, "Description is required").max(500),
  capacity: z.coerce.number().int().positive("Capacity must be greater than 0"),
  dailyRate: z.coerce.number().positive("Daily rate must be greater than 0"),
});

type RoomTypeFormInput = z.input<typeof roomTypeSchema>;
type RoomTypeFormValues = z.output<typeof roomTypeSchema>;

export function RoomTypeFormPage() {
  const { hotelId: hotelIdParam, id } = useParams<{ hotelId: string; id: string }>();
  const hotelId = Number(hotelIdParam);
  const roomTypeId = id ? Number(id) : undefined;
  const isEditMode = roomTypeId !== undefined;
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const { data: existingRoomType } = useQuery({
    queryKey: ["room-types", roomTypeId],
    queryFn: () => getRoomType(roomTypeId!),
    enabled: isEditMode,
  });

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<RoomTypeFormInput, unknown, RoomTypeFormValues>({
    resolver: zodResolver(roomTypeSchema),
  });

  useEffect(() => {
    if (existingRoomType) {
      reset({
        name: existingRoomType.name,
        description: existingRoomType.description,
        capacity: existingRoomType.capacity,
        dailyRate: existingRoomType.dailyRate,
      });
    }
  }, [existingRoomType, reset]);

  const mutation = useMutation({
    mutationFn: (values: RoomTypeFormValues) =>
      isEditMode ? updateRoomType(roomTypeId!, values) : createRoomType(hotelId, values),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["hotels", hotelId, "room-types"] });
      navigate(`/hotels/${hotelId}/room-types`);
    },
  });

  function onSubmit(values: RoomTypeFormValues) {
    mutation.mutate(values);
  }

  return (
    <main className="flex min-h-screen flex-col items-center justify-center gap-4">
      <h1 className="text-2xl font-semibold">{isEditMode ? "Edit Room Type" : "New Room Type"}</h1>
      <form onSubmit={handleSubmit(onSubmit)} className="flex w-full max-w-sm flex-col gap-3">
        <div>
          <label htmlFor="name" className="block text-sm font-medium">
            Name
          </label>
          <input id="name" type="text" className="w-full rounded border px-3 py-2" {...register("name")} />
          {errors.name && <p className="text-sm text-red-600">{errors.name.message}</p>}
        </div>
        <div>
          <label htmlFor="description" className="block text-sm font-medium">
            Description
          </label>
          <input
            id="description"
            type="text"
            className="w-full rounded border px-3 py-2"
            {...register("description")}
          />
          {errors.description && <p className="text-sm text-red-600">{errors.description.message}</p>}
        </div>
        <div>
          <label htmlFor="capacity" className="block text-sm font-medium">
            Capacity
          </label>
          <input id="capacity" type="number" className="w-full rounded border px-3 py-2" {...register("capacity")} />
          {errors.capacity && <p className="text-sm text-red-600">{errors.capacity.message}</p>}
        </div>
        <div>
          <label htmlFor="dailyRate" className="block text-sm font-medium">
            Daily Rate
          </label>
          <input
            id="dailyRate"
            type="number"
            step="0.01"
            className="w-full rounded border px-3 py-2"
            {...register("dailyRate")}
          />
          {errors.dailyRate && <p className="text-sm text-red-600">{errors.dailyRate.message}</p>}
        </div>
        <button
          type="submit"
          disabled={mutation.isPending}
          className="rounded bg-blue-600 px-4 py-2 text-white disabled:opacity-50"
        >
          {mutation.isPending ? "Saving..." : "Save"}
        </button>
      </form>
    </main>
  );
}
