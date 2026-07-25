import { useEffect } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useNavigate, useParams } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { createHotel, getHotel, updateHotel } from "../features/hotels/hotelService";

const hotelSchema = z.object({
  name: z.string().min(1, "Name is required").max(200),
  address: z.string().min(1, "Address is required").max(300),
  city: z.string().min(1, "City is required").max(100),
  phone: z.string().min(1, "Phone is required").max(20),
});

type HotelFormValues = z.infer<typeof hotelSchema>;

export function HotelFormPage() {
  const { id } = useParams<{ id: string }>();
  const hotelId = id ? Number(id) : undefined;
  const isEditMode = hotelId !== undefined;
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const { data: existingHotel } = useQuery({
    queryKey: ["hotels", hotelId],
    queryFn: () => getHotel(hotelId!),
    enabled: isEditMode,
  });

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<HotelFormValues>({
    resolver: zodResolver(hotelSchema),
  });

  useEffect(() => {
    if (existingHotel) {
      reset({
        name: existingHotel.name,
        address: existingHotel.address,
        city: existingHotel.city,
        phone: existingHotel.phone,
      });
    }
  }, [existingHotel, reset]);

  const mutation = useMutation({
    mutationFn: (values: HotelFormValues) =>
      isEditMode ? updateHotel(hotelId!, values) : createHotel(values),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["hotels"] });
      navigate("/hotels");
    },
  });

  function onSubmit(values: HotelFormValues) {
    mutation.mutate(values);
  }

  return (
    <main className="flex min-h-screen flex-col items-center justify-center gap-4">
      <h1 className="text-2xl font-semibold">{isEditMode ? "Edit Hotel" : "New Hotel"}</h1>
      <form onSubmit={handleSubmit(onSubmit)} className="flex w-full max-w-sm flex-col gap-3">
        <div>
          <label htmlFor="name" className="block text-sm font-medium">
            Name
          </label>
          <input id="name" type="text" className="w-full rounded border px-3 py-2" {...register("name")} />
          {errors.name && <p className="text-sm text-red-600">{errors.name.message}</p>}
        </div>
        <div>
          <label htmlFor="address" className="block text-sm font-medium">
            Address
          </label>
          <input id="address" type="text" className="w-full rounded border px-3 py-2" {...register("address")} />
          {errors.address && <p className="text-sm text-red-600">{errors.address.message}</p>}
        </div>
        <div>
          <label htmlFor="city" className="block text-sm font-medium">
            City
          </label>
          <input id="city" type="text" className="w-full rounded border px-3 py-2" {...register("city")} />
          {errors.city && <p className="text-sm text-red-600">{errors.city.message}</p>}
        </div>
        <div>
          <label htmlFor="phone" className="block text-sm font-medium">
            Phone
          </label>
          <input id="phone" type="text" className="w-full rounded border px-3 py-2" {...register("phone")} />
          {errors.phone && <p className="text-sm text-red-600">{errors.phone.message}</p>}
        </div>
        <button
          type="submit"
          disabled={isSubmitting}
          className="rounded bg-blue-600 px-4 py-2 text-white disabled:opacity-50"
        >
          {isSubmitting ? "Saving..." : "Save"}
        </button>
      </form>
    </main>
  );
}
